using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageSeqCapture
{
    class Program
    {
        public static class Globals
        {
            // camera
            public static uEye.Camera Camera;
            public static IntPtr displayHandle = IntPtr.Zero;
            public static int s32MemID;
            public static int s32SeqId;
            public static volatile bool bLive = false;
            public static int CamWidth;
            public static int CamHeight;

            public static int MaxImages = 100;
            public static double FrameRate = 400.0;
            public static int Counter = 0;
            public static List<System.Drawing.Bitmap> Bmps = new List<System.Drawing.Bitmap>();
            public static List<ulong> TimeStampTicks = new List<ulong>();

        }

        public class WorkerAcquire
        {
            // this method will be called when the thread is started.

            public void DoWork()
            {
                uint u32Timeout = 100;

                uEye.Defines.Status statusRet = 0;

                while (!_shouldStopAcquire)
                {
                    //get oldest buffer out of the queue
                    if (Globals.bLive)
                    {
                        statusRet = Globals.Camera.Memory.Sequence.WaitForNextImage(u32Timeout, out Globals.s32MemID, out Globals.s32SeqId);
                        if (statusRet == uEye.Defines.Status.Success)
                        {
                            // got a new image
                            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(Globals.CamWidth, Globals.CamHeight);
                            statusRet = Globals.Camera.Memory.CopyToBitmap(Globals.s32MemID, out bmp);
                            if (statusRet != uEye.Defines.Status.Success)
                            {
                                Console.WriteLine("Copy to Bitmap failed");
                            }

                            uEye.Types.ImageInfo info;
                            statusRet = Globals.Camera.Information.GetImageInfo(Globals.s32MemID, out info);
                            if (statusRet != uEye.Defines.Status.Success)
                            {
                                Console.WriteLine("Get Image Info failed");
                            }


                            Globals.Bmps.Add(bmp);
                            Globals.TimeStampTicks.Add(info.TimestampTick);

                            Globals.Counter++;

                            if (Globals.Counter > Globals.MaxImages)
                            {
                                statusRet = Globals.Camera.Acquisition.Stop();
                                if (statusRet != uEye.Defines.Status.Success)
                                {
                                    Console.WriteLine("Stop Live Video failed");
                                }
                                else
                                {
                                    Globals.bLive = false;
                                    Console.WriteLine("Stop Live Video");
                                    for (int i = 0; i < Globals.Bmps.Count; i++)
                                    {
                                        Globals.Bmps[i].Save("cap" + Globals.TimeStampTicks[i].ToString() + ".bmp");
                                    }

                                }
                            }

                            Globals.Camera.Memory.Sequence.Unlock(Globals.s32MemID);
                        }
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(100);
                    }

                }
                //terminating gracefully
            }

            public void RequestStopAcquire()
            {
                _shouldStopAcquire = true;
            }
            // volatile is used as hint to the compiler that this data member will be accessed by multiple threads.
            private volatile bool _shouldStopAcquire;

        }


        static void Main(string[] args)
        {
            // our camera
            Globals.Camera = new uEye.Camera();

            // Create thread for image acquisiition and start it
            WorkerAcquire workerAcquireObject = new WorkerAcquire();
            System.Threading.Thread workerAcquireThread = new System.Threading.Thread(workerAcquireObject.DoWork);
            workerAcquireThread.Start();


            // open Camera
            uEye.Defines.Status statusRet = 0;
            statusRet = Globals.Camera.Init();
            if (statusRet != uEye.Defines.Status.Success)
            {
                Console.WriteLine("Camera initializing failed");
                Environment.Exit(-1);
            }

            {
                double fps = Globals.FrameRate;
                statusRet = Globals.Camera.Timing.Framerate.Set(fps);
                if (statusRet != uEye.Defines.Status.Success)
                {
                    Console.WriteLine("Set frame rate failed");
                    Environment.Exit(-1);
                }
            }
            {
                Rectangle rect;
                statusRet = Globals.Camera.Size.AOI.Get(out rect);
                if (statusRet != uEye.Defines.Status.Success) throw new Exception("failed to get AOI.");


                Globals.CamWidth = rect.Width;
                Globals.CamHeight = rect.Height;

            }

            //GUI
            uEye.Types.SensorInfo sensorInfo;
            Globals.Camera.Information.GetSensorInfo(out sensorInfo);
            Console.WriteLine(sensorInfo.SensorName);



            // allocate inages buffers for 1 second of image queue
            double fFrameRate;
            Globals.Camera.Timing.Framerate.Get(out fFrameRate);
            int nNumberOfBuffers = (int)(fFrameRate);
            for (int i = 0; i < nNumberOfBuffers; i++)
            {
                int s32MemID;
                statusRet = Globals.Camera.Memory.Allocate(out s32MemID, false);
                if (statusRet == uEye.Defines.Status.Success)
                {
                    Globals.Camera.Memory.Sequence.Add(s32MemID);
                }
                else
                {
                    // exit, buffes are freed automatically
                    Console.WriteLine("Allocate Memory failed");
                    Environment.Exit(-1);
                }
            }
            statusRet = Globals.Camera.Memory.Sequence.InitImageQueue();


            // start live video
            statusRet = Globals.Camera.Acquisition.Capture();
            if (statusRet != uEye.Defines.Status.Success)
            {
                Console.WriteLine("Start Live Video failed");
            }
            else
            {
                Globals.bLive = true;
            }

        }
    }
}
