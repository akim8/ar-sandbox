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
        if (cameraManager != null)
        {
            cameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        // https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@3.0/manual/cpu-camera-image.html#asynchronously-convert-to-grayscale-and-color

        XRCameraImage image;
        if (!cameraManager.TryGetLatestImage(out image))
        {
            Debug.Log("Could not get camera image");
            return;
        }

        StartCoroutine(ProcessImage(image));

        image.Dispose();
    }

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
        var tex = new Texture2D(
                conversionParams.outputDimensions.x,
                conversionParams.outputDimensions.y,
                conversionParams.outputFormat,
                false);

        tex.LoadRawTextureData(rawData);
        tex.Apply();

        // https://answers.unity.com/questions/712673/how-to-encode-an-image-to-a-base64-string.html

        byte[] bytes = tex.EncodeToPNG();

        string imageBase64 = Convert.ToBase64String(bytes);

        displayText.text = imageBase64.Length.ToString();
        //displayText.text = await SendStringToPastecode(imageBase64);
        displayText.text = await SendStringToVisionAI(imageBase64, "OBJECT_LOCALIZATION");
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

        return responseString;
    }
}
