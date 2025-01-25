using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgcodecsModule;
using OpenCVForUnity.ImgprocModule;
#if UNITY_EDITOR
using OpenCVForUnity.UnityUtils.Helper.Editor;
#endif
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace OpenCVForUnity.UnityUtils.Helper
{
    /// <summary>
    /// A helper component class for loading an image file using OpenCV's <c>Imgcodecs.imread</c> method and converting it to an OpenCV <c>Mat</c> format.
    /// </summary>
    /// <remarks>
    /// The <c>Image2MatHelper</c> class reads an image file from the specified path and converts it to an OpenCV <c>Mat</c> object using <c>Imgcodecs.imread</c>. 
    /// This component simplifies the process of loading images for applications that require image processing in Unity with OpenCV.
    /// 
    /// This component is particularly useful for static image processing tasks, enabling seamless integration of OpenCV-based algorithms with image files directly within Unity.
    /// 
    /// <strong>Note:</strong> By setting outputColorFormat to GRAY or BGR, processing that does not include extra color conversion is performed.
    /// </remarks>
    /// <example>
    /// Attach this component to a GameObject and call <c>GetMat()</c> to retrieve the latest image frame in <c>Mat</c> format. 
    /// </example>
    public class Image2MatHelper : MonoBehaviour, IImageSource2MatHelper, IMatUpdateFPSProvider
    {

#if UNITY_EDITOR
        [OpenCVForUnityRuntimeDisable]
#endif
        [SerializeField, FormerlySerializedAs("requestedImageFilePath"), TooltipAttribute("Set the image file path, relative to the starting point of the \"StreamingAssets\" folder, or absolute path.")]
        protected string _requestedImageFilePath = string.Empty;

        /// <summary>
        /// Set the image file path, relative to the starting point of the "StreamingAssets" folder, or absolute path.
        /// </summary>
        public virtual string requestedImageFilePath
        {
            get { return _requestedImageFilePath; }
            set
            {
                if (_requestedImageFilePath != value)
                {
                    _requestedImageFilePath = value;
                    if (hasInitDone)
                        Initialize(IsPlaying());
                    else if (isInitWaiting)
                        Initialize(autoPlayAfterInitialize);
                }
            }
        }


#if UNITY_EDITOR
        [OpenCVForUnityRuntimeDisable]
#endif
        [SerializeField, FormerlySerializedAs("requestedMatUpdateFPS"), TooltipAttribute("Set the frame rate of camera.")]
        protected float _requestedMatUpdateFPS = 30f;

        /// <summary>
        /// Sets the frame rate at which the Mat is updated. (interval at which the DidUpdateThisFrame() method becomes true).
        /// </summary>
        public virtual float requestedMatUpdateFPS
        {
            get { return _requestedMatUpdateFPS; }
            set
            {
                float _value = Mathf.Clamp(value, -1f, float.MaxValue);
                if (_requestedMatUpdateFPS != _value)
                {
                    _requestedMatUpdateFPS = _value;
                    fpsManager = new FpsManager(_requestedMatUpdateFPS);
                }
            }
        }


#if UNITY_EDITOR
        [OpenCVForUnityRuntimeDisable]
#endif
        [SerializeField, FormerlySerializedAs("outputColorFormat"), TooltipAttribute("Select the output color format.")]
        protected Source2MatHelperColorFormat _outputColorFormat = Source2MatHelperColorFormat.BGR;

        /// <summary>
        /// Select the output color format.
        /// </summary>
        public virtual Source2MatHelperColorFormat outputColorFormat
        {
            get { return _outputColorFormat; }
            set
            {
                if (_outputColorFormat != value)
                {
                    _outputColorFormat = value;
                    if (hasInitDone)
                        Initialize(IsPlaying());
                    else if (isInitWaiting)
                        Initialize(autoPlayAfterInitialize);
                }
            }
        }


#if UNITY_EDITOR
        [OpenCVForUnityRuntimeDisable]
#endif
        [SerializeField, FormerlySerializedAs("timeoutFrameCount"), TooltipAttribute("The number of frames before the initialization process times out.")]
        protected int _timeoutFrameCount = 1500;

        /// <summary>
        /// The number of frames before the initialization process times out.
        /// </summary>
        public virtual int timeoutFrameCount
        {
            get { return _timeoutFrameCount; }
            set { _timeoutFrameCount = (int)Mathf.Clamp(value, 0f, float.MaxValue); }
        }


#if UNITY_EDITOR
        [OpenCVForUnityRuntimeDisable]
#endif
        [SerializeField, FormerlySerializedAs("repeat"), TooltipAttribute("Indicate whether to play this image in a repeat.")]
        protected bool _repeat = true;

        /// <summary>
        /// Indicate whether to play this image in a repeat.
        /// </summary>
        public virtual bool repeat
        {
            get { return _repeat; }
            set
            {
                _repeat = value;
                repeatedCount = 0;
            }
        }


        [SerializeField, FormerlySerializedAs("onInitialized"), TooltipAttribute("UnityEvent that is triggered when this instance is initialized.")]
        protected UnityEvent _onInitialized;

        /// <summary>
        /// UnityEvent that is triggered when this instance is initialized.
        /// </summary>
        public UnityEvent onInitialized
        {
            get => _onInitialized;
            set => _onInitialized = value;
        }


        [SerializeField, FormerlySerializedAs("onDisposed"), TooltipAttribute("UnityEvent that is triggered when this instance is disposed.")]
        protected UnityEvent _onDisposed;

        /// <summary>
        /// UnityEvent that is triggered when this instance is disposed.
        /// </summary>
        public UnityEvent onDisposed
        {
            get => _onDisposed;
            set => _onDisposed = value;
        }


        [SerializeField, FormerlySerializedAs("onErrorOccurred"), TooltipAttribute("UnityEvent that is triggered when this instance is error Occurred.")]
        protected Source2MatHelperErrorUnityEvent _onErrorOccurred;

        /// <summary>
        /// UnityEvent that is triggered when this instance is error Occurred.
        /// </summary>
        public Source2MatHelperErrorUnityEvent onErrorOccurred
        {
            get => _onErrorOccurred;
            set => _onErrorOccurred = value;
        }

        /// <summary>
        /// isPlaying
        /// </summary>
        protected bool isPlaying = false;

        /// <summary>
        /// Whether the mat that can be obtained with the GetMat method has been updated in this frame.
        /// This flag is changed after waiting until WaitForEndOfFrame in the coroutine.
        /// </summary>
        protected bool didUpdateThisFrame = false;

        /// <summary>
        /// The frame mat.
        /// </summary>
        protected Mat frameMat;

        /// <summary>
        /// The base mat.
        /// </summary>
        protected Mat baseMat;


        protected System.Object _imageBufferMatLockObject = new System.Object();
        protected Mat _imageBufferMat;

        /// <summary>
        /// The image buffer mat.
        /// </summary>
        protected Mat imageBufferMat
        {
            get { lock (_imageBufferMatLockObject) return _imageBufferMat; }
            set { lock (_imageBufferMatLockObject) _imageBufferMat = value; }
        }

        /// <summary>
        /// repeatedCount
        /// </summary>
        private int repeatedCount;

        /// <summary>
        /// The base color format.
        /// </summary>
        protected Source2MatHelperColorFormat baseColorFormat = Source2MatHelperColorFormat.BGR;

        /// <summary>
        /// Indicates whether this instance is waiting for initialization to complete.
        /// </summary>
        protected bool isInitWaiting = false;

        /// <summary>
        /// Indicates whether this instance has been initialized.
        /// </summary>
        protected bool hasInitDone = false;

        /// <summary>
        /// The initialization coroutine.
        /// </summary>
        protected IEnumerator initCoroutine;

        /// <summary>
        /// The get file path coroutine.
        /// </summary>
        protected IEnumerator getFilePathCoroutine;

        /// <summary>
        /// imageFileFullPath
        /// </summary>
        protected string imageFileFullPath;

        /// <summary>
        /// If set to true play after completion of initialization.
        /// </summary>
        protected bool autoPlayAfterInitialize;

        /// <summary>
        /// FPS Manager
        /// </summary>
        private FpsManager fpsManager;

        /// <summary>
        /// cancellationTokenSource
        /// </summary>
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// cancellationToken
        /// </summary>
        private CancellationToken cancellationToken;

        /// <summary>
        /// currentTask
        /// </summary>
        private Task currentTask;

        /// <summary>
        /// resetEvent
        /// </summary>
        private ManualResetEventSlim resetEvent;

        /// <summary>
        /// The waitForEndOfFrameCoroutine
        /// </summary>
        protected IEnumerator waitForEndOfFrameCoroutine;

        protected virtual void OnValidate()
        {
            _requestedMatUpdateFPS = Mathf.Clamp(_requestedMatUpdateFPS, -1f, float.MaxValue);
            _timeoutFrameCount = (int)Mathf.Clamp(_timeoutFrameCount, 0f, float.MaxValue);
        }

        // Update is called once per frame
        protected virtual void Update()
        {
            if (hasInitDone)
            {
                if (!isPlaying)
                    return;

                if (!_repeat && repeatedCount > 0)
                    return;

                // Update FPSManager and execute process at regular intervals.
                if (fpsManager.Update(Time.deltaTime, CallReadFrame))
                {
                    repeatedCount++;
                }
            }
        }

        protected virtual void CallReadFrame()
        {

#if UNITY_WEBGL

            ReadFrame();

#else

            // Skip if Task.Run is already running.
            if (currentTask == null || currentTask.IsCompleted)
            {
                // Execute the process in background thread
                currentTask = Task.Run(() =>
                {

                    //Debug.Log("Background thread: " + Thread.CurrentThread.ManagedThreadId);

                    try
                    {

                        // Wait until data is copied from imageBufferMat to baseMat.
                        resetEvent.Wait();

                        cancellationToken.ThrowIfCancellationRequested();

                        ReadFrame();

                    }
                    catch (OperationCanceledException)
                    {
                        Debug.Log("Task was canceled.");
                    }
                }, cancellationToken);
            }
#endif
        }

        protected virtual void ReadFrame()
        {
            imageBufferMat = Imgcodecs.imread(imageFileFullPath, baseColorFormat == Source2MatHelperColorFormat.GRAY ? Imgcodecs.IMREAD_GRAYSCALE : Imgcodecs.IMREAD_COLOR);

            // Data update
            resetEvent.Reset();

        }

        protected virtual IEnumerator _WaitForEndOfFrameCoroutine()
        {

            while (true)
            {
                yield return new WaitForEndOfFrame();

                if (!resetEvent.IsSet)
                {
                    // If the data has been updated during this frame, the data is copied to baseMat.

                    imageBufferMat.copyTo(baseMat);

                    // Data copying to baseMat completed.
                    resetEvent.Set();

                    didUpdateThisFrame = true;
                    //Debug.Log("start didUpdateThisFrame " + didUpdateThisFrame + " " + Time.frameCount);
                }
                else
                {
                    // After one frame, the didUpdateThisFrame flag is set to false.
                    didUpdateThisFrame = false;
                    //Debug.Log("end didUpdateThisFrame " + didUpdateThisFrame + " " + Time.frameCount);
                }
            }
        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        protected virtual void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// Initialize this instance.
        /// </summary>
        /// <param name="autoPlay">If set to <c>true</c> play after completion of initialization.</param>
        public virtual void Initialize(bool autoPlay = true)
        {
            if (isInitWaiting)
            {
                CancelInitCoroutine();
                ReleaseResources();
            }

            autoPlayAfterInitialize = autoPlay;
            if (_onInitialized == null)
                _onInitialized = new UnityEvent();
            if (_onDisposed == null)
                _onDisposed = new UnityEvent();
            if (_onErrorOccurred == null)
                _onErrorOccurred = new Source2MatHelperErrorUnityEvent();

            initCoroutine = _Initialize();
            StartCoroutine(initCoroutine);
        }

        /// <summary>
        /// Initialize this instance.
        /// </summary>
        /// <param name="requestedImageFilePath">Requested image file path.</param>
        /// <param name="autoPlay">If set to <c>true</c> play after completion of initialization.</param>
        public virtual void Initialize(string requestedImageFilePath, bool autoPlay = true)
        {
            if (isInitWaiting)
            {
                CancelInitCoroutine();
                ReleaseResources();
            }

            _requestedImageFilePath = requestedImageFilePath;
            autoPlayAfterInitialize = autoPlay;
            if (_onInitialized == null)
                _onInitialized = new UnityEvent();
            if (_onDisposed == null)
                _onDisposed = new UnityEvent();
            if (_onErrorOccurred == null)
                _onErrorOccurred = new Source2MatHelperErrorUnityEvent();

            initCoroutine = _Initialize();
            StartCoroutine(initCoroutine);
        }

        /// <summary>
        /// Initialize this instance by coroutine.
        /// </summary>
        protected virtual IEnumerator _Initialize()
        {
            if (hasInitDone)
            {
                CancelWaitForEndOfFrameCoroutine();
                ReleaseResources();

                if (_onDisposed != null)
                    _onDisposed.Invoke();
            }

            isInitWaiting = true;

            // Wait one frame before starting initialization process
            yield return null;


            bool hasFilePathCoroutineCompleted = false;
            imageFileFullPath = string.Empty;

            Uri uri;
            if (Uri.TryCreate(requestedImageFilePath, UriKind.Absolute, out uri))
            {
                hasFilePathCoroutineCompleted = true;
                imageFileFullPath = uri.OriginalString;
            }
            else
            {
                getFilePathCoroutine = Utils.getFilePathAsync(requestedImageFilePath, (result) =>
                {
                    hasFilePathCoroutineCompleted = true;
                    imageFileFullPath = result;
                });

                StartCoroutine(getFilePathCoroutine);
            }

            int initFrameCount = 0;
            bool isTimeout = false;

            while (true)
            {
                if (initFrameCount > timeoutFrameCount)
                {
                    isTimeout = true;
                    break;
                }
                else if (hasFilePathCoroutineCompleted)
                {
                    if (string.IsNullOrEmpty(imageFileFullPath))
                    {
                        isInitWaiting = false;
                        initCoroutine = null;
                        getFilePathCoroutine = null;

                        if (_onErrorOccurred != null)
                            _onErrorOccurred.Invoke(Source2MatHelperErrorCode.IMAGE_FILE_NOT_EXIST, requestedImageFilePath);

                        yield break;
                    }

                    if (outputColorFormat == Source2MatHelperColorFormat.GRAY)
                        baseColorFormat = Source2MatHelperColorFormat.GRAY;

                    imageBufferMat = Imgcodecs.imread(imageFileFullPath, baseColorFormat == Source2MatHelperColorFormat.GRAY ? Imgcodecs.IMREAD_GRAYSCALE : Imgcodecs.IMREAD_COLOR);
                    baseMat = imageBufferMat.clone();

                    if (baseMat.empty())
                    {
                        isInitWaiting = false;
                        initCoroutine = null;
                        getFilePathCoroutine = null;

                        if (_onErrorOccurred != null)
                            _onErrorOccurred.Invoke(Source2MatHelperErrorCode.IMAGE_FILE_CANT_OPEN, imageFileFullPath);

                        yield break;
                    }

                    if (baseColorFormat == outputColorFormat)
                    {
                        frameMat = baseMat.clone();
                    }
                    else
                    {
                        frameMat = new Mat(baseMat.rows(), baseMat.cols(), CvType.CV_8UC(Source2MatHelperUtils.Channels(outputColorFormat)));
                    }

                    repeatedCount = 0;

                    // Initialize FpsManager
                    fpsManager = new FpsManager(_requestedMatUpdateFPS);

                    // Create token source for cancellation
                    cancellationTokenSource = new CancellationTokenSource();
                    cancellationToken = cancellationTokenSource.Token;

                    resetEvent = new ManualResetEventSlim(true);

                    Debug.Log("Image2MatHelper:: " + " filePath:" + requestedImageFilePath + " width:" + frameMat.width() + " height:" + frameMat.height());

                    isInitWaiting = false;
                    hasInitDone = true;
                    initCoroutine = null;
                    getFilePathCoroutine = null;

                    isPlaying = autoPlayAfterInitialize;

                    // Data copying to baseMat completed.
                    resetEvent.Set();

                    didUpdateThisFrame = true;
                    //Debug.Log("start didUpdateThisFrame " + didUpdateThisFrame);

                    // Start a coroutine and wait for WaitForEndOfFrame. If already started, stop and then start.
                    if (waitForEndOfFrameCoroutine != null) StopCoroutine(waitForEndOfFrameCoroutine);
                    waitForEndOfFrameCoroutine = _WaitForEndOfFrameCoroutine();
                    StartCoroutine(waitForEndOfFrameCoroutine);


                    if (_onInitialized != null)
                        _onInitialized.Invoke();

                    break;
                }
                else
                {
                    initFrameCount++;
                    yield return null;
                }
            }

            if (isTimeout)
            {
                isInitWaiting = false;
                initCoroutine = null;
                getFilePathCoroutine = null;

                if (_onErrorOccurred != null)
                    _onErrorOccurred.Invoke(Source2MatHelperErrorCode.TIMEOUT, string.Empty);
            }
        }

        /// <summary>
        /// Indicate whether this instance has been initialized.
        /// </summary>
        /// <returns><c>true</c>, if this instance has been initialized, <c>false</c> otherwise.</returns>
        public virtual bool IsInitialized()
        {
            return hasInitDone;
        }

        /// <summary>
        /// Start the image.
        /// </summary>
        public virtual void Play()
        {
            if (hasInitDone)
            {
                isPlaying = true;
                fpsManager.IsPaused = false;
            }
        }

        /// <summary>
        /// Pause the image.
        /// </summary>
        public virtual void Pause()
        {
            if (hasInitDone)
            {
                isPlaying = false;
                fpsManager.IsPaused = true;
            }
        }

        /// <summary>
        /// Stop the image.
        /// </summary>
        public virtual void Stop()
        {
            if (hasInitDone)
            {
                isPlaying = false;
                fpsManager.IsPaused = true;
            }
        }

        /// <summary>
        /// Indicate whether the image is currently playing.
        /// </summary>
        /// <returns><c>true</c>, if the image is playing, <c>false</c> otherwise.</returns>
        public virtual bool IsPlaying()
        {
            return hasInitDone ? isPlaying : false;
        }

        /// <summary>
        /// Indicate whether the image is paused.
        /// </summary>
        /// <returns><c>true</c>, if the image is paused, <c>false</c> otherwise.</returns>
        public virtual bool IsPaused()
        {
            return hasInitDone ? isPlaying : false;
        }

        /// <summary>
        /// Return the active image device name.
        /// </summary>
        /// <returns>The active image device name.</returns>
        public virtual string GetDeviceName()
        {
            return "OpenCV_Imgcodecs.imread";
        }

        /// <summary>
        /// Return the image width.
        /// </summary>
        /// <returns>The image width.</returns>
        public virtual int GetWidth()
        {
            if (!hasInitDone)
                return -1;
            return frameMat.width();
        }

        /// <summary>
        /// Return the image height.
        /// </summary>
        /// <returns>The image height.</returns>
        public virtual int GetHeight()
        {
            if (!hasInitDone)
                return -1;
            return frameMat.height();
        }

        /// <summary>
        /// Return the image base color format.
        /// </summary>
        /// <returns>The image base color format.</returns>
        public virtual Source2MatHelperColorFormat GetBaseColorFormat()
        {
            return baseColorFormat;
        }

        /// <summary>
        /// Return the frame rate at which the Mat is updated. (interval at which the DidUpdateThisFrame() method becomes true).
        /// </summary>
        /// <returns>The active camera framerate.</returns>
        public virtual float GetMatUpdateFPS()
        {
            return hasInitDone ? _requestedMatUpdateFPS : -1f;
        }

        /// <summary>
        /// Use this to check if the Mat has changed since the last frame. Since it would not make sense to do expensive video processing in each Update call, check this value before doing any processing.
        /// </summary>
        /// <returns><c>true</c>, if the Mat has been updated <c>false</c> otherwise.</returns>
        public virtual bool DidUpdateThisFrame()
        {
            if (!hasInitDone)
                return false;

            return didUpdateThisFrame;
        }

        /// <summary>
        /// Get the mat of the current frame.
        /// </summary>
        /// <remarks>
        /// The Mat object's type is 'CV_8UC4' or 'CV_8UC3' or 'CV_8UC1' (ColorFormat is determined by the outputColorFormat setting).
        /// Please do not dispose of the returned mat as it will be reused.
        /// </remarks>
        /// <returns>The mat of the current frame.</returns>
        public virtual Mat GetMat()
        {
            if (!hasInitDone)
            {
                return null;
            }

            if (baseColorFormat == outputColorFormat)
            {
                baseMat.copyTo(frameMat);
            }
            else
            {
                Imgproc.cvtColor(baseMat, frameMat, Source2MatHelperUtils.ColorConversionCodes(baseColorFormat, outputColorFormat));
            }

            return frameMat;
        }

        /// <summary>
        /// Cancel Init Coroutine.
        /// </summary>
        protected virtual void CancelInitCoroutine()
        {
            if (getFilePathCoroutine != null)
            {
                StopCoroutine(getFilePathCoroutine);
                ((IDisposable)getFilePathCoroutine).Dispose();
                getFilePathCoroutine = null;
            }

            if (initCoroutine != null)
            {
                StopCoroutine(initCoroutine);
                ((IDisposable)initCoroutine).Dispose();
                initCoroutine = null;
            }
        }

        /// <summary>
        /// Cancel WaitForEndOfFrame Coroutine.
        /// </summary>
        protected virtual void CancelWaitForEndOfFrameCoroutine()
        {
            if (waitForEndOfFrameCoroutine != null)
            {
                StopCoroutine(waitForEndOfFrameCoroutine);
                ((IDisposable)waitForEndOfFrameCoroutine).Dispose();
                waitForEndOfFrameCoroutine = null;
            }
        }

        /// <summary>
        /// To release the resources.
        /// </summary>
        protected virtual void ReleaseResources()
        {
            isInitWaiting = false;
            hasInitDone = false;

            isPlaying = false;
            didUpdateThisFrame = false;

            if (frameMat != null)
            {
                frameMat.Dispose();
                frameMat = null;
            }
            if (baseMat != null)
            {
                baseMat.Dispose();
                baseMat = null;
            }
            if (imageBufferMat != null)
            {
                imageBufferMat.Dispose();
                imageBufferMat = null;
            }

        }

        /// <summary>
        /// Releases all resource used by the <see cref="Image2MatHelper"/> object.
        /// </summary>
        /// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Image2MatHelper"/>. The
        /// <see cref="Dispose"/> method leaves the <see cref="Image2MatHelper"/> in an unusable state. After
        /// calling <see cref="Dispose"/>, you must release all references to the <see cref="Image2MatHelper"/> so
        /// the garbage collector can reclaim the memory that the <see cref="Image2MatHelper"/> was occupying.</remarks>
        public virtual void Dispose()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            if (resetEvent != null)
            {
                resetEvent.Dispose();
            }

            if (isInitWaiting)
            {
                CancelInitCoroutine();
                CancelWaitForEndOfFrameCoroutine();
                ReleaseResources();
            }
            else if (hasInitDone)
            {
                CancelWaitForEndOfFrameCoroutine();
                ReleaseResources();

                if (_onDisposed != null)
                    _onDisposed.Invoke();
            }
        }
    }
}