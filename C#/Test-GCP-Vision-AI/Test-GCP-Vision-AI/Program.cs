using System;
using Google.Cloud.Vision.V1;


namespace Test_GCP_Vision_AI
{
    class Program
    {
        static void Main(string[] args)
        {
            // Instantiates a client
            var client = ImageAnnotatorClient.Create();

            // Load the image file into memory
            Console.WriteLine("Enter the path to an image");
            string filePath = Console.ReadLine();
            var image = Image.FromFile(filePath);

            // Performs label detection on the image file
            var response = client.DetectLabels(image);

            foreach (var annotation in response)
            {
                if (annotation.Description != null)
                    Console.WriteLine(annotation.Description);
            }
        }
    }
}
