// Copyright(c) 2017 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraScript : MonoBehaviour
{
    /// <summary>
    ///     detect interval.
    /// </summary>
    private static int FRAME_INTERVAL = 20;

    /// <summary>
    ///     face Detect object list.
    /// </summary>
    private readonly List<Image> _faceObjects = new List<Image>();
    private readonly List<RawImage> _faceImages = new List<RawImage>();
    
    /// <summary>
    ///     Canvas Object
    /// </summary>
    public GameObject Canvas;

    /// <summary>
    ///     Tempalte Image Object.
    /// </summary>
    public Image FaceObject;
    public RawImage FaceImage;

    /// <summary>
    ///     Text object for Inidicate Detected Data
    /// </summary>
    public Text TextData;
    

    /// <summary>
    ///     FaceDetect object.
    /// </summary>
    private FaceDetectBase Service { get; set; }

    private bool processing_flag = false;
    private Texture2D facetex = null;

    // Update is called once per frame
    private void Start()
    {
    }

    // Use this for initialization
    private void Update()
    {
        if (Time.frameCount % FRAME_INTERVAL == 0)
        {
            if (Service == null)
            {
#if UNITY_EDITOR
                // For Debug.when this application execute by unity,call this. 
                Service = new FaceDetectStub();
#else
// execute For HoloLens. 
                System.Diagnostics.Debug.WriteLine("fuga");
                Service = UWPBridgeServiceManager.Instance.GetService<FaceDetectBase>();
                System.Diagnostics.Debug.WriteLine("hemi");
                TextData.text = "Service Initialized.";
#endif

                //Service.OnDetected = SetFaceObject;
                Service.OnDetected = SetFaceImage;
                System.Diagnostics.Debug.WriteLine("hemi");
                Service.Start();
            }

            Service.DetectFace();
        }
    }

    public void SetFaceImage(List<FaceInformation> list)
    {
        var dif = _faceImages.Count - list.Count;
        if (dif > 0)
            for (var i = 0; i < dif; i++)
            {
                Destroy(_faceImages[0]);
                _faceImages[0] = null;
                _faceImages.RemoveAt(0);
            }
        else if (dif < 0)
            for (var i = 0; i < -1 * dif; i++)
            {
                var instantiate = Instantiate(FaceImage);
                _faceImages.Add(instantiate);
            }
        TextData.text = "";
        System.Diagnostics.Debug.WriteLine("facecount: " + _faceImages.Count.ToString());
        if (_faceImages.Count > 0)
        {
            for (var i = 0; i < 1; i++)
            {
                if (facetex == null)
                {
                    facetex = new Texture2D((int)list[i].Width, (int)list[i].Height, TextureFormat.RGBA32, false);
                }
                if (processing_flag)
                {

                }
                else
                {
                    Texture2D tex = new Texture2D((int)list[i].Width, (int)list[i].Height, TextureFormat.RGBA32, false);
                    var colorArray = new Color32[list[i].RawData.Length / 4];
                    var byteArray = list[i].RawData;
                    for (var j = 0; j < byteArray.Length; j += 4)
                    {
                        var color = new Color32(byteArray[j + 0], byteArray[j + 1], byteArray[j + 2], byteArray[j + 3]);
                        colorArray[j / 4] = color;
                    }
                    tex.SetPixels32(colorArray);
                    tex.Apply();
                    processing_flag = true;
                    StartCoroutine(HttpPost(tex));
                }
                /*
                Texture2D tex = new Texture2D((int)list[i].Width, (int)list[i].Height, TextureFormat.BGRA32, false);
                var colorArray = new Color32[list[i].RawData.Length / 4];
                var byteArray = list[i].RawData;
                System.Diagnostics.Debug.WriteLine("bytelen: " + byteArray.Length.ToString());
                System.Diagnostics.Debug.WriteLine("size: " + ((int)(list[i].Width * list[i].Height * 4)).ToString());
                for (var j = 0; j < byteArray.Length; j+=4)
                {
                    var color = new Color32(byteArray[j + 0], byteArray[j + 1], byteArray[j + 2], byteArray[j + 3]);
                    colorArray[j / 4] = color;
                }
                tex.SetPixels32(colorArray);
                tex.Apply();
                */
                var faceImage = _faceImages[i];

                faceImage.texture = facetex;
                var faceDetectedImageRectTransform = faceImage.GetComponent(typeof(RectTransform)) as RectTransform;

                var canvasRectTransform = Canvas.GetComponent(typeof(RectTransform)) as RectTransform;
                if (canvasRectTransform == null)
                    return;
                var w = canvasRectTransform.sizeDelta.x / Service.FrameSizeWidth;
                var h = canvasRectTransform.sizeDelta.y / Service.FrameSizeHeight;

                System.Diagnostics.Debug.WriteLine(canvasRectTransform.sizeDelta.x.ToString() + ":" + canvasRectTransform.sizeDelta.y.ToString());
                System.Diagnostics.Debug.WriteLine("w: " + w.ToString() + ", h: " + h.ToString());
                if (faceDetectedImageRectTransform == null)
                    return;
                faceDetectedImageRectTransform.transform.parent = Canvas.transform;
                faceDetectedImageRectTransform.sizeDelta = new Vector2(list[i].Width, list[i].Height);
                //faceImage.SetNativeSize();

                var scale = 1.5f;
                //Sets face's maeker position.
                var texx = (list[i].X - 0.5f * list[i].Width) * w;
                //var texx = (list[i].X - 0.5f * list[i].Width + 0.5f * list[i].Width * 1.5f) * w;
                var texy = (list[i].Y - 0.5f * list[i].Height + list[i].Height * scale) * w;
                //var texy = (list[i].Y - 0.5f * list[i].Height + 0.5f * list[i].Height * 1.5f) * w;
                /*faceDetectedImageRectTransform.position =
                    Canvas.transform.TransformPoint(
                        list[i].X*w  - canvasRectTransform.sizeDelta.x/2,
                        //texx - canvasRectTransform.sizeDelta.x / 2,
                        -list[i].Y*w  + canvasRectTransform.sizeDelta.y/2,
                        //-texy + canvasRectTransform.sizeDelta.y / 2,
                        0f);*/
                faceDetectedImageRectTransform.position =
                    Canvas.transform.TransformPoint(
                        list[i].X - Service.FrameSizeWidth / 2,
                        -list[i].Y + Service.FrameSizeHeight / 2,
                        0f);
                faceDetectedImageRectTransform.localScale = new Vector3(scale, scale, 1.0f);
                faceImage.transform.rotation = new Quaternion(0f, 0f, 0f, 0f);
                //faceImage.transform.localScale = new Vector3(1f, 1f, 1f);
                TextData.text += string.Format("X=[{0}],Y=[{1}],Width=[{2}],Height=[{3}]\n", list[i].X, list[i].Y,
                    list[i].Width, list[i].Height);
            }
        }
    }

    public Vector3 calc_position(Vector3 orgpos)
    {
        int w = Service.FrameSizeWidth;
        int h = Service.FrameSizeHeight;
        float fov = 22.5f;
        float dist = 0.5f * w / Mathf.Tan(fov * Mathf.PI / 180.0f);
        Vector3 pos = new Vector3(orgpos[0], orgpos[1], dist);
        var canvasRectTransform = Canvas.GetComponent(typeof(RectTransform)) as RectTransform;
        Vector3 newpos = pos * (canvasRectTransform.position[2] / pos[2]);
        return newpos;
    }

    /// <summary>
    ///     when detected faces in screenshot,Sets Face's marker on canvas..
    /// </summary>
    /// <param name="list"></param>
    public void SetFaceObject(List<FaceInformation> list)
    {
        var dif = _faceObjects.Count - list.Count;
        if (dif > 0)
            for (var i = 0; i < dif; i++)
            {
                Destroy(_faceObjects[0]);
                _faceObjects[0] = null;
                _faceObjects.RemoveAt(0);
            }
        else if (dif < 0)
            for (var i = 0; i < -1 * dif; i++)
            {
                var instantiate = Instantiate(FaceObject);
                _faceObjects.Add(instantiate);
            }
        TextData.text = "";
        for (var i = 0; i < _faceObjects.Count; i++)
        {
            var faceObject = _faceObjects[i];

            var faceDetectedImageRectTransform = faceObject.GetComponent(typeof(RectTransform)) as RectTransform;

            var canvasRectTransform = Canvas.GetComponent(typeof(RectTransform)) as RectTransform;
            if (canvasRectTransform == null)
                return;
            var w = canvasRectTransform.sizeDelta.x / Service.FrameSizeWidth;
            var h = canvasRectTransform.sizeDelta.y / Service.FrameSizeHeight;

            if (faceDetectedImageRectTransform == null)
                return;
            faceDetectedImageRectTransform.transform.parent = Canvas.transform;
            faceDetectedImageRectTransform.sizeDelta = new Vector2(list[i].Width, list[i].Height);

            //Sets face's maeker position.
            faceDetectedImageRectTransform.position =
                Canvas.transform.TransformPoint(
                    list[i].X*w  - canvasRectTransform.sizeDelta.x/2,
                    -list[i].Y*h  + canvasRectTransform.sizeDelta.y/2,
                    0f);
            faceObject.transform.rotation = new Quaternion(0f, 0f, 0f, 0f);
            faceObject.transform.localScale = new Vector3(1f, 1f, 1f);
            TextData.text += string.Format("X=[{0}],Y=[{1}],Width=[{2}],Height=[{3}]\n", list[i].X, list[i].Y,
                list[i].Width, list[i].Height);
        }
    }
    IEnumerator HttpPost(Texture2D tex)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", tex.EncodeToJPG());
        //WWW www = new WWW("http://133.11.216.158:5000/test", form);
        WWW www = new WWW("http://hoop.jsk.imi.i.u-tokyo.ac.jp", form);
        yield return StartCoroutine(CheckTimeOut(www, 3.0f));
        Debug.Log("Posted");
        if (www.error != null)
        {
            Debug.Log("HttpPost NG: " + www.error);
        }
        else if (www.isDone)
        {
            Debug.Log("isDone");
            facetex.LoadImage(www.bytes);
        }
        Debug.Log("finish");
        processing_flag = false;
    }
    IEnumerator CheckTimeOut(WWW www, float timeout)
    {
        float requestTime = Time.time;
        while(!www.isDone)
        {
            if (Time.time - requestTime < timeout)
                yield return null;
            else
            {
                Debug.Log("Timeout");
                break;
            }
        }
        yield return null;
    }
}

/// <summary>
///     when this application is Debug on Unity,Sets StubData.
/// </summary>
internal class FaceDetectStub : FaceDetectBase
{

    public FaceDetectStub()
    {
        FrameSizeWidth = 1920;
        FrameSizeHeight = 1200;
    }

    public override void DetectFace()
    {
        var faceInformations = new List<FaceInformation>();
        faceInformations.Add(new FaceInformation
        {
            X = 827,
            Y = 510,
            Width = 37, //(float) (300/1920*0.44),
            Height = 37 //(float) (500/1200*0.24)
        });
        faceInformations.Add(new FaceInformation
        {
            X = 746,
            Y = 660,
            Width = 59, //(float) (300/1920*0.44),
            Height = 37 //(float) (500/1200*0.24)
        });
        OnDetected(faceInformations);
    }
}

/// <summary>
///     Represents a class that face detect processing.
/// </summary>
public abstract class FaceDetectBase : IUWPBridgeService
{
    public delegate void SetFaceObject(List<FaceInformation> list);

    /// <summary>
    ///  Gets or Sets width of screenshot  size.
    /// </summary>
    public int FrameSizeWidth;

    /// <summary>
    ///  Gets or Sets height of screenshot  size.
    /// </summary>
    public int FrameSizeHeight;


    /// <summary>
    /// Gets or sets the action to be performed after face detect processing.
    /// </summary>
    public SetFaceObject OnDetected;

    /// <summary>
    /// Perform face detect. 
    /// </summary>
    public abstract void DetectFace();
    public virtual void Start()
    {
        System.Diagnostics.Debug.WriteLine("Start");
    }
}

/// <summary>
///     Represents a class that face detected data.
/// </summary>
public class FaceInformation
{
    /// <summary>
    ///     Set and Get face detect Height.
    /// </summary>
    public float Height;

    /// <summary>
    ///     Set and Get face detect Width.
    /// </summary>
    public float Width;

    /// <summary>
    ///     Set and Get face detect posiotion X.
    /// </summary>
    public float X;

    /// <summary>
    ///     Set and Get face detect posiotion Y.
    /// </summary>
    public float Y;

    public byte[] RawData;
}