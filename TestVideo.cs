using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FFmpeg.AutoGen;
using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using Unity.Collections;

//video stream base ffmpeg.autogen
public unsafe class TestVideo : MonoBehaviour
{
    [SerializeField]
    RawImage _texture;
    [SerializeField]
    Button _playBtn;
    [SerializeField]
    Button _stopBtn;
    [SerializeField]
    Button _pauseBtn;
    [SerializeField]
    Button _switchBtn;

    //RenderTexture _rdMap;
    Texture2D _tmp2D;
    RectTransform _imgRtt;
    Rect _imgRt;
    int _width;
    int _height;
    string _url2 = "rtmp://live.hkstv.hk.lxdns.com/live/hks1";
    string _url1 = "rtmp://ns8.indexforce.com/home/mystream";
    //string _url1 = "rtmp://rtmp01open.ys7.com/openlive/f01018a141094b7fa138b9d0b856507b";
    string _url;
    bool _Pause = false;
    bool _Stop = false;
    bool _playing = false;
    byte[] _updateframe = new byte[51200];
    int FRAME_RATE = 25;
    float _startPlayT = 0;
    //Bitmap _bitmap;
    int _videoIndex = -1;
    int _audioIndex = -1;
    //IntPtr _imgPointer;
    Thread _videotd;

    private void Awake()
    {
        if (_texture != null)
        {

            _imgRtt = _texture.rectTransform;
            _imgRt = _imgRtt.rect;
            _width = (int)_imgRtt.sizeDelta.x;
            _height = (int)_imgRtt.sizeDelta.y;
            //if (_rdMap == null)
            //    _rdMap = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32);
            //_texture.texture = _rdMap;
            if (_tmp2D == null)
                _tmp2D = new Texture2D(_width, _height, TextureFormat.RGB24, false);
        }
        //_updateframe = new byte[_width * _height];
        _playBtn.onClick.AddListener(() =>
        {
            if (string.IsNullOrEmpty(_url))
                _url = _url2;
            StartPlay(_url);
        });
        _stopBtn.onClick.AddListener(() =>
        {
            _Stop = true;
            //_videotd.Join();
            //_videotd.Interrupt();
        });
        _pauseBtn.onClick.AddListener(() =>
        {
            _Pause = true;
        });
        _switchBtn.onClick.AddListener(() =>
        {
            if (!string.IsNullOrEmpty(_url))
            {
                if (_url == _url1)
                    StartPlay(_url2);
                else
                    StartPlay(_url1);
            }
        });
        string _dllpath = Application.dataPath;
        string _dllpathsys = Environment.CurrentDirectory + "\\Assets\\Plugins\\ffmpeg";
        Debug.Log(_dllpath + "\r\n" + _dllpathsys);
        SetDllDirectory(_dllpathsys);

    }
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (_playing && _updateframe != null)
        {
            _startPlayT += Time.deltaTime;
            if (_startPlayT >= 1 / (float)FRAME_RATE) {
                UpdateImage(_updateframe, _width, _height);
                _startPlayT -= 1 / (float)FRAME_RATE;
            }
        }
    }

    void OnTextureResized()
    {

    }

    void StartPlay(string _liveurl)
    {
        if (_videotd != null && _videotd.IsAlive)
        {
            if (_url == _liveurl)
            {
                return;
            }
            _videotd.Interrupt();
        }
        _url = _liveurl;

        if (_videotd == null)
        {
            _videotd = new Thread(() => { InitFFmpegVideo(_url, _width, _height); });
            _videotd.IsBackground = true;
            _videotd.Start();
        }else
        {
            
        }
        _startPlayT = 0;
        _Stop = false;
        _Pause = false;

    }
    void InitFFmpegVideo(string _url, int _width, int _height)
    {
        AVFormatContext* _avfmtcontext;
        AVCodecParameters* _avcodecParas;
        AVCodecContext* _avcodeccontext;
        AVCodec* _avcodec;
        AVFrame* _avframe;

        ffmpeg.avformat_network_init();
        _avfmtcontext = ffmpeg.avformat_alloc_context();
        if (ffmpeg.avformat_open_input(&_avfmtcontext, _url, null, null) != 0)
        {
            Debug.Log("url error");
            return;
        }
        if (ffmpeg.avformat_find_stream_info(_avfmtcontext, null) < 0)
        {
            Debug.Log("video stream error");
            return;
        }
        for (int i = 0; i < _avfmtcontext->nb_streams; i++)
        {
            if (_avfmtcontext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                _videoIndex = i;
                continue;
            }
            else if (_avfmtcontext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
            {
                _audioIndex = i;
                continue;
            }
            else
            {

            }
        }
        if (_videoIndex < 0)
        {
            Debug.Log("stream has no video");
            return;
        }
        _avcodecParas = _avfmtcontext->streams[_videoIndex]->codecpar;
        _avcodec = ffmpeg.avcodec_find_decoder(_avcodecParas->codec_id);
        if (_avcodec == null)
        {
            Debug.Log("failed get avdecodec");
            return;
        }
        _avcodeccontext = ffmpeg.avcodec_alloc_context3(null);
        if (ffmpeg.avcodec_parameters_to_context(_avcodeccontext, _avcodecParas) < 0)
        {
            Debug.Log("failed get context");
            return;
        }
        if (ffmpeg.avcodec_open2(_avcodeccontext, _avcodec, null) < 0)
        {
            Debug.Log("open avcodec failed");
            return;
        }
        _avframe = ffmpeg.av_frame_alloc();
        //_avframeRGB = ffmpeg.av_frame_alloc();
        int _bys = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_RGB24, _avcodeccontext->width, _avcodeccontext->height, 1);//AV_PIX_FMT_BGR24
        //byte* buffer = (byte*)ffmpeg.av_malloc((ulong)_bys * sizeof(byte));
        byte* _destbuffer = (byte*)Marshal.AllocHGlobal(_bys);
        byte_ptrArray4 _destdata = new byte_ptrArray4();
        int_array4 _linesize = new int_array4();
        ffmpeg.av_image_fill_arrays(ref _destdata, ref _linesize, _destbuffer, AVPixelFormat.AV_PIX_FMT_RGB24, _width, _height, 1);

        //FILE *output = fopen("out.rgb", "wb+");       

        AVPacket _pkt;
        ffmpeg.av_init_packet(&_pkt);
        //bgr24 ->auto-> rgb
        SwsContext* img_ctx = ffmpeg.sws_getContext(_avcodeccontext->width, _avcodeccontext->height, _avcodeccontext->pix_fmt, _width, _height, AVPixelFormat.AV_PIX_FMT_BGR24, ffmpeg.SWS_BILINEAR, null, null, null);
        //var _ms = new MemoryStream();
        int _frameId = 0;
        while (ffmpeg.av_read_frame(_avfmtcontext, &_pkt) >= 0)
        {
            if (_Pause)
                continue;
            if (_Stop)
                break;
            if (_pkt.stream_index == _videoIndex)
            {
                if (ffmpeg.avcodec_send_packet(_avcodeccontext, &_pkt) != 0)
                {
                    continue;
                }
                if (ffmpeg.avcodec_receive_frame(_avcodeccontext, _avframe) == 0)
                {
                    ffmpeg.sws_scale(img_ctx, _avframe->data, _avframe->linesize, 0, _avcodeccontext->height, _destdata, _linesize);
                    //Marshal.Copy((IntPtr)_destdata[0], _updateframe, 0, _width * _height); 
                    //File.WriteAllBytes(Environment.CurrentDirectory + "\\temp\\" +_frameId +".png", _updateframe);

                    using (Bitmap _bitmap = new Bitmap(_width, _height, _linesize[0], PixelFormat.Format24bppRgb, (IntPtr)_destdata[0]))
                    {
                        //_bitmap.Save($"tempimg\\frame.{_frameId:D8}.jpg", ImageFormat.Jpeg);
                        using (var _ms = new MemoryStream())
                        {
                            _bitmap.Save(_ms, ImageFormat.Png);
                            _updateframe = _ms.GetBuffer();
                        }
                        Debug.Log("playing: " + _updateframe[10] + ":" + _updateframe[1000] + ":" + _updateframe[2000]);
                        _playing = true;
                    }
                    _frameId++;
                    //if (_frameId > 200)
                    //    break;
                }
            }

        }

        ffmpeg.av_packet_unref(&_pkt);
        ffmpeg.av_frame_free(&_avframe);
        ffmpeg.sws_freeContext(img_ctx);
        ffmpeg.avformat_close_input(&_avfmtcontext);
        Marshal.FreeHGlobal((IntPtr)_destbuffer);
    }

    private static byte[] GetBitmapData(Bitmap frameBitmap)
    {
        var bitmapData = frameBitmap.LockBits(new Rectangle(Point.Empty, frameBitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var length = bitmapData.Stride * bitmapData.Height;
            var data = new byte[length];
            Marshal.Copy(bitmapData.Scan0, data, 0, length);
            return data;
        }
        finally
        {
            frameBitmap.UnlockBits(bitmapData);
        }
    }
    void UpdateImage(byte[] _srcData, int _w, int _h)
    {
        //if (_tmp2D == null)
        //    _tmp2D = new Texture2D(_width, _height, TextureFormat.RGB24, false);
        _tmp2D.LoadImage(_srcData);
        //_tmp2D.LoadRawTextureData(_srcData);
        _tmp2D.Apply();
        _texture.texture = _tmp2D;
    }

    //void testfixed()
    //{
    //    const int _num = 10;
    //    byte[] _ints = new byte[_num];
    //    for(int i = 0; i < 5000; i++)
    //    {
    //        new object();
    //    }
    //    fixed(byte* _ptr = _ints)
    //    {
    //        for(int i = 0; i < _num; i++)
    //        {
    //            _ptr[i] = (byte)new System.Random(Guid.NewGuid().GetHashCode()).Next(0, 255);
    //        }
    //    }
    //    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
    //    Debug.Log("GC output: ");
    //    Array.ForEach(_ints, _ => Debug.Log(_));
    //}
    [DllImport("kernel32", EntryPoint = "CopyMemory", SetLastError = false)]
    public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

    [DllImport("kernel32", SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);
}
