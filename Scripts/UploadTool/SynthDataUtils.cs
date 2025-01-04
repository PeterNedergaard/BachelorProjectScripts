using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Drawing.Imaging;
using System.Drawing;

namespace AwsAzureAiApp
{
    public class SynthDataUtils
    {
        public ICustomVisionTrainingClient trainingClient;
        public Guid projectId;
        private InferenceSession _session;
        Dictionary<string, Guid> tagMap;


        public SynthDataUtils(ICustomVisionTrainingClient trainingClient, Guid projectId)
        {
            string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "onnx", "model.onnx");
            _session = new InferenceSession(modelPath);

            this.trainingClient = trainingClient;
            this.projectId = projectId;
            tagMap = new Dictionary<string, Guid>();
        }


        public List<ImageFileCreateEntry> PrepareImages(List<Annotation> annotations)
        {
            try
            {
                Console.WriteLine("Starting to prepare images...");
                var images = new List<ImageFileCreateEntry>();

                Tag backgroundTag = trainingClient.GetTags(projectId).FirstOrDefault(t => t.Name == "background");

                // If the background tag does not exist, create it
                if (backgroundTag == null)
                {
                    backgroundTag = trainingClient.CreateTag(projectId, "background");
                }

                foreach (var annotation in annotations)
                {
                    foreach (var capture in annotation.captures)
                    {
                        var regions = new List<Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models.Region>();
                        var tagIds = new List<Guid>();
                        var imageFilePath = Path.Combine(annotation.FolderName, capture.filename);
                        var imageBytes = File.ReadAllBytes(imageFilePath);

                        using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageBytes))
                        {
                            int imageWidth = image.Width;
                            int imageHeight = image.Height;

                            foreach (var data in capture.annotations)
                            {
                                // Take into account the possibility of no objects in the image
                                if (data.values == null)
                                {
                                    images.Add(new ImageFileCreateEntry
                                    {
                                        Name = capture.filename,
                                        Contents = imageBytes,
                                        TagIds = new List<Guid> { backgroundTag.Id }
                                    });

                                    continue;
                                }

                                foreach (var bbox in data.values)
                                {
                                    double left = bbox.origin[0] / imageWidth;
                                    double top = bbox.origin[1] / imageHeight;
                                    double width = bbox.dimension[0] / imageWidth;
                                    double height = bbox.dimension[1] / imageHeight;

                                    // Normalize and clamp values
                                    left = Math.Max(0, Math.Min(left, 1));
                                    top = Math.Max(0, Math.Min(top, 1));
                                    width = Math.Max(0, Math.Min(width, 1 - left));
                                    height = Math.Max(0, Math.Min(height, 1 - top));

                                    // Add tag ID to the dictionary, if not already present
                                    var tag = bbox.labelName;
                                    if (!tagMap.ContainsKey(tag))
                                    {
                                        tagMap[tag] = GetTagId(tag);
                                    }

                                    tagIds.Add(tagMap[tag]);

                                    regions.Add(new Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models.Region
                                    {
                                        TagId = tagMap[tag],
                                        Left = left,
                                        Top = top,
                                        Width = width,
                                        Height = height
                                    });
                                }
                            }

                            images.Add(new ImageFileCreateEntry
                            {
                                Name = capture.filename,
                                Contents = imageBytes,
                                TagIds = tagIds.Distinct().ToList(),
                                Regions = regions
                            });
                        }
                    }
                }
                Console.WriteLine("Image preparation complete.");
                return images;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during PrepareImages: " + ex.Message);
                return null;
            }
        }


        public static List<string> LoadLabelNames(string definitionFilePath)
        {
            try
            {
                Console.WriteLine($"Loading label names from {definitionFilePath}");
                string jsonText = File.ReadAllText(definitionFilePath);
                var definitionRoot = JsonConvert.DeserializeObject<AnnotationDefinitionRoot>(jsonText);
                var labelNames = new List<string>();
                foreach (var definition in definitionRoot.annotationDefinitions)
                {
                    foreach (var spec in definition.spec)
                    {
                        labelNames.Add(spec.label_name);
                    }
                }
                Console.WriteLine("Label names loaded successfully.");
                return labelNames.Distinct().ToList();  // Ensure unique label names
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during LoadLabelNames: " + ex.Message);
                return null;
            }
        }


        public async Task ProcessData(string rootDirectory)
        {
            try
            {
                Console.WriteLine($"Starting data processing in root directory: {rootDirectory}");
                var directories = Directory.GetDirectories(rootDirectory);
                int batchSize = 10;
                var processingTasks = new List<Task>();

                for (int i = 0; i < directories.Length; i += batchSize)
                {
                    var batchDirectories = directories.Skip(i).Take(batchSize);
                    var task = ProcessBatchAsync(batchDirectories, i / batchSize + 1, (int)Math.Ceiling(directories.Length / (double)batchSize));
                    processingTasks.Add(task);
                }

                // Wait for all processing tasks to complete
                await Task.WhenAll(processingTasks);
                Console.WriteLine("Data processing complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during ProcessData: " + ex.Message);
            }
        }


        private async Task ProcessBatchAsync(IEnumerable<string> batchDirectories, int batchNumber, int totalBatches)
        {
            var annotationsBatch = new List<Annotation>();

            foreach (var subDir in batchDirectories)
            {
                var jsonFilePath = Path.Combine(subDir, "step0.frame_data.json");
                var annotation = LoadAnnotationFromJson(jsonFilePath, subDir);
                annotationsBatch.Add(annotation);
            }

            Console.WriteLine($"Processing batch {batchNumber} of {totalBatches}: {annotationsBatch.Count} annotations");

            var images = PrepareImages(annotationsBatch);
            await UploadImages(images, batchNumber, totalBatches);
        }


        public async Task UploadImages(List<ImageFileCreateEntry> images, int batchNumber, int totalBatches)
        {
            if (images == null)
            {
                Console.WriteLine("No images to upload in batch " + batchNumber);
                return;
            }

            ImageFileCreateBatch batch = null;

            try
            {
                Console.WriteLine($"Uploading batch {batchNumber} of {totalBatches} with {images.Count} images...");

                batch = new ImageFileCreateBatch(images);
                await trainingClient.CreateImagesFromFilesAsync(projectId, batch).ConfigureAwait(false);

                Console.WriteLine($"Batch {batchNumber} of {totalBatches} upload COMPLETE.");
            }
            catch (CustomVisionErrorException e)
            {
                Console.WriteLine("Failed to upload image batch due to an API error.");
                Console.WriteLine($"Error Code: {e.Body.Code}");
                Console.WriteLine($"Error Message: {e.Body.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during image batch uploads: {ex.Message}");
            }
            finally
            {
                // Dispose images if they are disposable or contain disposable resources
                foreach (var image in images)
                {
                    (image as IDisposable)?.Dispose();
                }

                // Clear the list to release the references to the objects
                images.Clear();

                // Dispose of the batch if it is disposable
                if (batch is IDisposable disposableBatch)
                {
                    disposableBatch.Dispose();
                }
            }
        }


        public Guid GetTagId(string labelName)
        {
            try
            {
                Console.WriteLine($"Retrieving tag ID for label: {labelName}");
                var tags = trainingClient.GetTags(projectId);
                var tag = tags.FirstOrDefault(t => t.Name == labelName);
                if (tag == null)
                {
                    Console.WriteLine($"No existing tag found for {labelName}, creating new tag.");
                    tag = trainingClient.CreateTag(projectId, labelName);
                }
                return tag.Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during GetTagId: " + ex.Message);
                return Guid.Empty;
            }
        }


        public static Annotation LoadAnnotationFromJson(string jsonFilePath, string folderPath)
        {
            try
            {
                string jsonText = File.ReadAllText(jsonFilePath);
                var annotation = JsonConvert.DeserializeObject<Annotation>(jsonText);
                annotation.FolderName = folderPath;
                return annotation;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during LoadAnnotationFromJson: " + ex.Message);
                return null;
            }
        }


        public (int width, int height) GetImageDimensions(byte[] imageBytes)
        {
            using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageBytes))
            {
                return (image.Width, image.Height);
            }
        }


        public async Task DeleteImagesInBatches(Guid projectId)
        {
            int batchSize = 100;
            int maxNumberOfBatches = 10;  // Control the maximum number of fetches in flight
            List<Task<IList<Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models.Image>>> fetchTasks = new List<Task<IList<Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models.Image>>>();
            var deleteTasks = new List<Task>();
            int skip = 0;
            bool moreImages = true;

            Console.WriteLine("Starting image deletion process...");

            // Continue fetching and deleting images in batches until there are no more images to delete
            while (moreImages)
            {
                // Prepare and initiate fetch tasks in parallel up to a maximum number
                for (int i = 0; i < maxNumberOfBatches; i++)
                {
                    var fetchTask = FetchImages(projectId, skip, batchSize);
                    fetchTasks.Add(fetchTask);
                    skip += batchSize;
                    Console.WriteLine($"Fetch task initiated for batch starting at index {skip - batchSize}.");
                }

                // Wait for all fetch tasks to complete
                var fetchedImages = await Task.WhenAll(fetchTasks);

                // Prepare deletion tasks for all fetched images that are not empty
                foreach (var imageList in fetchedImages)
                {
                    if (imageList.Count >= 1)
                    {
                        var deleteTask = trainingClient.DeleteImagesAsync(projectId, imageList.Select(i => i.Id).ToList());
                        deleteTasks.Add(deleteTask);
                        Console.WriteLine($"Deletion task started for {imageList.Count} images.");
                    } else
                    {
                        moreImages = false;
                    }
                }
            }

            // Await all delete operations to complete
            await Task.WhenAll(deleteTasks);
            Console.WriteLine("Batch deleted successfully.");

            // Proceed to delete all tags
            await DeleteTags(projectId);
            Console.WriteLine("All tags deleted successfully.");
        }


        private async Task<IList<Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models.Image>> FetchImages(Guid projectId, int skip, int take)
        {
            var images = await trainingClient.GetImagesAsync(projectId, take: take, skip: skip);
            if (images.Count > 0)
            {
                Console.WriteLine($"Fetched {images.Count} images starting at index {skip}.");
            }
            else
            {
                Console.WriteLine($"No images found at skip value {skip}. No deletion necessary for this batch.");
            }
            return images;
        }


        public async Task DeleteTags(Guid projectId)
        {
            try
            {
                var tags = await trainingClient.GetTagsAsync(projectId);

                foreach (var tag in tags)
                {
                    await trainingClient.DeleteTagAsync(projectId, tag.Id);
                    Console.WriteLine($"Tag {tag.Name} deleted successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete tags: {ex.Message}");
            }
        }



        public class Annotation
        {
            public int frame;
            public List<Capture> captures;
            public List<Metric> metrics;
            public string FolderName;
        }

        public class Capture
        {
            public string filename;
            public List<AnnotationData> annotations;
        }

        public class AnnotationData
        {
            public List<BoundingBox> values;
        }

        public class BoundingBox
        {
            public string labelName;
            public float[] origin;
            public float[] dimension;
        }

        public class Metric
        {
            public string id;
            public int value;
        }

        public class AnnotationDefinitionRoot
        {
            public List<AnnotationDefinition> annotationDefinitions;
        }

        public class AnnotationDefinition
        {
            public List<LabelSpec> spec;
        }

        public class LabelSpec
        {
            public int label_id;
            public string label_name;
        }


    }
}
