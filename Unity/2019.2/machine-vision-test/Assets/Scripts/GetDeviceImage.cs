using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class GetDeviceImage : MonoBehaviour
{
    public TMP_Text displayText;
    public Button pasteButton;
    public GameObject testOrbPrefab;

    private NativeArray<byte> rawData;
    private XRCameraImageConversionParams conversionParams;

    private TextAsset apiKeyFile;

    private static readonly HttpClient client = new HttpClient();

    const int imageScaleDenom = 3;

    ARCameraManager cameraManager;

    // Start is called before the first frame update
    void Start()
    {
        pasteButton.onClick.AddListener(PasteButtonAction);

        // load gcp api key
        if (Resources.Load("gcp.apikey"))
        {
            apiKeyFile = (TextAsset)Resources.Load("gcp.apikey", typeof(TextAsset));
        }
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void OnEnable()
    {
        // find the cameraManager to get the camera feed from

        cameraManager = GameObject.Find("AR Camera").GetComponent<ARCameraManager>();

        if (cameraManager != null)
        {
            cameraManager.frameReceived += OnCameraFrameReceived;
        }
        else
        {
            Debug.LogError("Could not get ARCameraManager component.");
        }
    }

    private void OnDisable()
    {
        // stop getting camera feed

        if (cameraManager != null)
        {
            cameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    // converts camera feed to byte array every frame - could be optimized?
    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        // https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@3.0/manual/cpu-camera-image.html#asynchronously-convert-to-grayscale-and-color

        XRCameraImage image;
        if (!cameraManager.TryGetLatestImage(out image))
        {
            Debug.Log("Could not get camera image");
            return;
        }

        StartCoroutine(ProcessImage(image)); // send image to coroutine to asynchronously process

        image.Dispose();
    }

    // converts image into native byte array
    IEnumerator ProcessImage(XRCameraImage image) 
    {
        var request = image.ConvertAsync(new XRCameraImageConversionParams
        {
            // Use the full image
            inputRect = new RectInt(0, 0, image.width, image.height),

            // Downsample
            outputDimensions = new Vector2Int(image.width / imageScaleDenom, image.height / imageScaleDenom),

            // Color image format
            outputFormat = TextureFormat.RGB24,

            // Flip across the Y axis
            transformation = CameraImageTransformation.MirrorY
        });

        while (!request.status.IsDone())
            yield return null;

        if (request.status != AsyncCameraImageConversionStatus.Ready) {
            Debug.LogErrorFormat("Request failed with status {0}", request.status);

            request.Dispose();
            yield break;
        }

        rawData = request.GetData<byte>();
        conversionParams = request.conversionParams;

        request.Dispose();
    }

    async void PasteButtonAction()
    {
        // convert native byte array into texture - if a way to convert
        // NativeArray<byte> into byte[] was found, then this step could be
        // skipped and resources could be saved.

        var tex = new Texture2D(
                conversionParams.outputDimensions.x,
                conversionParams.outputDimensions.y,
                conversionParams.outputFormat,
                false);

        tex.LoadRawTextureData(rawData);
        tex.Apply();

        // get byte array from texture
        // https://answers.unity.com/questions/712673/how-to-encode-an-image-to-a-base64-string.html

        byte[] bytes = tex.EncodeToPNG();

        // convert byte array to base64 string
        string imageBase64 = Convert.ToBase64String(bytes);

        // send encoded image to API of choice

        displayText.text = imageBase64.Length.ToString(); // for loading, also good way to tell if request has surpassed character limit (I think it's 1,000,000 characters)
        //displayText.text = await SendStringToPastecode(imageBase64);
        //displayText.text = await SendStringToVisionAI(imageBase64, "OBJECT_LOCALIZATION"); // object recognition
        displayText.text = await SendStringToVisionAI(imageBase64, "TEXT_DETECTION");
    }

    private async Task<string> SendStringToPastecode(string text)
    {
        string url = "https://pastecode.xyz/api/create";
        var content = new Dictionary<string, string>();
        content.Add("text", text);
        content.Add("expire", "10");
        content.Add("private", "1");

        // form-data uri has a character limit, this works around it
        var encodedItems = content.Select(i => WebUtility.UrlEncode(i.Key) + "=" + WebUtility.UrlEncode(i.Value));
        var encodedContent = new StringContent(String.Join("&", encodedItems), null, "application/x-www-form-urlencoded");

        var response = await client.PostAsync(url, encodedContent);
        var responseString = await response.Content.ReadAsStringAsync();

        return responseString;
    }

    private async Task<string> SendStringToVisionAI(string text, string featureType = "LABEL_DETECTION", int maxResults = 10)
    {
        string url = "https://vision.googleapis.com/v1/images:annotate?key=";
        string json = "{\"requests\": [{\"image\": {\"content\": \"" + text + "\"},\"features\": [{\"maxResults\": " + maxResults + ",\"model\": \"\",\"type\": \"" + featureType + "\"}]}]}";

        var content = new StringContent(json, Encoding.UTF8, "application/json"); // convert string to HTTPcontent

        var response = await client.PostAsync(url + apiKeyFile.text, content);

        var responseString = await response.Content.ReadAsStringAsync();

        Debug.Log(responseString);

        if (!response.IsSuccessStatusCode)
        {
            return "Response status code: " + response.StatusCode.ToString();
        }

        // do something with response

        if (featureType == "TEXT_DETECTION")
        {
            /* TODO: create floating frames for detected text in AR
             *
             * get surface that object is on
             * create frame on surface based on coordinates from text detection (use raycasting from screenspace coordinates to surface)
             * attach labels with the detected text to each frame
             * cleanup (separate this into a new script/function)
             *
             * remember, response can be empty
             * { "responses": [ {} ] }
             * 
             */

            Debug.Log(responseString);

            // get just the textAnnotations from the response
            if (responseString.Contains("textAnnotations"))
            {
                // hardcoded way to shorten json response
                int begin = responseString.IndexOf("responses");
                int end = responseString.IndexOf("fullTextAnnotation");
                string alteredResponse = responseString.Remove(end - 1).Trim().Remove(0, begin + 13);
                alteredResponse = alteredResponse.Remove(alteredResponse.Length - 2) + "]}";

                Debug.Log(alteredResponse);
                Debug.Log(">>>>> Finished json string manip");

                TextAnnotationWrapper<TextAnnotation> taWrapper = JsonUtility.FromJson<TextAnnotationWrapper<TextAnnotation>>(alteredResponse);

                foreach (var x in taWrapper.textAnnotations)
                {
                    Debug.Log(x.description);
                }
            }

            //GameObject testOrb1 = Instantiate(testOrbPrefab, Camera.main.ScreenToWorldPoint(new Vector3(x, y)), Camera.main.transform.rotation); // only spawns object in center of screen... why?
        }

        return response.StatusCode.ToString();
    }
}

[Serializable]
public class TextAnnotationWrapper<TextAnnotation>
{
    public TextAnnotation[] textAnnotations;
}

[Serializable]
public class TextAnnotation {
    public string locale;
    public string description;
    public BoundingPoly boundingPoly;
}

[Serializable]
public class BoundingPoly
{
    public Vertex[] verticies = new Vertex[4];
}

[Serializable]
public class Vertex
{
    public int x = 0;
    public int y = 0;
}