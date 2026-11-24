using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using ArsStudio.Core.Scanning;
using ArsStudio.ViewModels;

namespace ArsStudio.Services.MacOS;

[SupportedOSPlatform("macos")]
public sealed partial class MacCameraService : ICameraService
{
    private const string AVFoundation = "/System/Library/Frameworks/AVFoundation.framework/AVFoundation";
    private const string CoreMedia = "/System/Library/Frameworks/CoreMedia.framework/CoreMedia";
    private const string CoreVideo = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";
    private const string SystemLibrary = "/usr/lib/libSystem.B.dylib";
    private const uint PixelFormatBgra = 0x42475241;
    private const ulong ReadOnlyLock = 1;
    private const int MaxFrameWidth = 960;
    private const int AuthorizationNotDetermined = 0;
    private const int AuthorizationAuthorized = 3;
    private const int BlockIsGlobal = 1 << 28;

    private static readonly string[] DeviceTypes =
    [
        "AVCaptureDeviceTypeBuiltInWideAngleCamera",
        "AVCaptureDeviceTypeExternal",
        "AVCaptureDeviceTypeExternalUnknown",
        "AVCaptureDeviceTypeContinuityCamera",
    ];

    private static Action<FrameImage>? s_frameArrived;
    private static TaskCompletionSource<bool>? s_access;
    private static IntPtr s_delegateClass;
    private static IntPtr s_accessBlock;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _uniqueIds = [];
    private IntPtr _session;
    private IntPtr _input;
    private IntPtr _output;
    private IntPtr _delegate;
    private IntPtr _queue;

    public unsafe IReadOnlyList<string> GetCameraNames()
    {
        var pool = ObjectiveC.PushPool();
        try
        {
            var mediaType = ObjectiveC.Constant(AVFoundation, "AVMediaTypeVideo");
            var types = new List<IntPtr>();
            foreach (var name in DeviceTypes)
            {
                var type = ObjectiveC.Constant(AVFoundation, name);
                if (type != IntPtr.Zero && !types.Contains(type))
                {
                    types.Add(type);
                }
            }

            var array = types.ToArray();
            IntPtr typeArray;
            fixed (IntPtr* items = array)
            {
                typeArray = ObjectiveC.Send(ObjectiveC.Class("NSArray"), "arrayWithObjects:count:", (IntPtr)items, array.Length);
            }

            var discovery = ObjectiveC.Send(
                ObjectiveC.Class("AVCaptureDeviceDiscoverySession"),
                "discoverySessionWithDeviceTypes:mediaType:position:",
                typeArray,
                mediaType,
                IntPtr.Zero);
            var devices = ObjectiveC.Send(discovery, "devices");
            var count = (int)ObjectiveC.Send(devices, "count");

            _uniqueIds.Clear();
            var names = new List<string>();
            for (var index = 0; index < count; index++)
            {
                var device = ObjectiveC.Send(devices, "objectAtIndex:", index);
                var name = ObjectiveC.ToManaged(ObjectiveC.Send(device, "localizedName"));
                var uniqueId = ObjectiveC.ToManaged(ObjectiveC.Send(device, "uniqueID"));
                if (name is null || uniqueId is null || _uniqueIds.ContainsKey(name))
                {
                    continue;
                }

                _uniqueIds[name] = uniqueId;
                names.Add(name);
            }

            return names;
        }
        finally
        {
            ObjectiveC.PopPool(pool);
        }
    }

    public async Task StartAsync(string name, Action<FrameImage> frameArrived)
    {
        await StopAsync();

        if (!_uniqueIds.TryGetValue(name, out var uniqueId))
        {
            throw new InvalidOperationException($"Videocamera {name} non trovata.");
        }

        if (!await RequestAccessAsync())
        {
            throw new UnauthorizedAccessException("Accesso alla videocamera negato.");
        }

        await Task.Run(() =>
        {
            lock (_gate)
            {
                Open(uniqueId);
                s_frameArrived = frameArrived;
                ObjectiveC.Send(_session, "startRunning");
            }
        });
    }

    public Task StopAsync() => Task.Run(Close);

    public void Dispose() => Close();

    private unsafe void Open(string uniqueId)
    {
        var pool = ObjectiveC.PushPool();
        try
        {
            var device = ObjectiveC.Send(ObjectiveC.Class("AVCaptureDevice"), "deviceWithUniqueID:", ObjectiveC.String(uniqueId));
            if (device == IntPtr.Zero)
            {
                throw new InvalidOperationException("Videocamera non disponibile.");
            }

            IntPtr error = IntPtr.Zero;
            var input = ObjectiveC.Send(ObjectiveC.Class("AVCaptureDeviceInput"), "deviceInputWithDevice:error:", device, (IntPtr)(&error));
            if (input == IntPtr.Zero)
            {
                throw new InvalidOperationException("Impossibile aprire la videocamera.");
            }

            _input = ObjectiveC.Retain(input);
            _session = ObjectiveC.Send(ObjectiveC.Send(ObjectiveC.Class("AVCaptureSession"), "alloc"), "init");

            var preset = ObjectiveC.Constant(AVFoundation, "AVCaptureSessionPreset640x480");
            if (preset != IntPtr.Zero && ObjectiveC.SendBool(_session, "canSetSessionPreset:", preset))
            {
                ObjectiveC.Send(_session, "setSessionPreset:", preset);
            }

            _output = ObjectiveC.Send(ObjectiveC.Send(ObjectiveC.Class("AVCaptureVideoDataOutput"), "alloc"), "init");
            var formatKey = ObjectiveC.Constant(CoreVideo, "kCVPixelBufferPixelFormatTypeKey");
            var format = ObjectiveC.Send(ObjectiveC.Class("NSNumber"), "numberWithUnsignedInt:", (IntPtr)PixelFormatBgra);
            var settings = ObjectiveC.Send(ObjectiveC.Class("NSDictionary"), "dictionaryWithObject:forKey:", format, formatKey);
            ObjectiveC.Send(_output, "setVideoSettings:", settings);
            ObjectiveC.SendFlag(_output, "setAlwaysDiscardsLateVideoFrames:", true);

            _delegate = ObjectiveC.Send(ObjectiveC.Send(DelegateClass(), "alloc"), "init");
            _queue = ObjectiveC.CreateQueue("ars.camera");
            ObjectiveC.Send(_output, "setSampleBufferDelegate:queue:", _delegate, _queue);

            if (!ObjectiveC.SendBool(_session, "canAddInput:", _input) || !ObjectiveC.SendBool(_session, "canAddOutput:", _output))
            {
                throw new InvalidOperationException("Configurazione della videocamera non supportata.");
            }

            ObjectiveC.Send(_session, "addInput:", _input);
            ObjectiveC.Send(_session, "addOutput:", _output);
        }
        catch
        {
            ReleaseAll();
            throw;
        }
        finally
        {
            ObjectiveC.PopPool(pool);
        }
    }

    private void Close()
    {
        lock (_gate)
        {
            s_frameArrived = null;
            if (_session != IntPtr.Zero)
            {
                ObjectiveC.Send(_session, "stopRunning");
            }

            ReleaseAll();
        }
    }

    private void ReleaseAll()
    {
        if (_output != IntPtr.Zero)
        {
            ObjectiveC.Send(_output, "setSampleBufferDelegate:queue:", IntPtr.Zero, IntPtr.Zero);
        }

        ObjectiveC.Release(_session);
        ObjectiveC.Release(_output);
        ObjectiveC.Release(_input);
        ObjectiveC.Release(_delegate);
        ObjectiveC.Release(_queue);
        _session = _output = _input = _delegate = _queue = IntPtr.Zero;
    }

    private static Task<bool> RequestAccessAsync()
    {
        var mediaType = ObjectiveC.Constant(AVFoundation, "AVMediaTypeVideo");
        var status = (int)ObjectiveC.Send(ObjectiveC.Class("AVCaptureDevice"), "authorizationStatusForMediaType:", mediaType);
        if (status != AuthorizationNotDetermined)
        {
            return Task.FromResult(status == AuthorizationAuthorized);
        }

        var access = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        s_access = access;
        ObjectiveC.Send(ObjectiveC.Class("AVCaptureDevice"), "requestAccessForMediaType:completionHandler:", mediaType, AccessBlock());
        return access.Task;
    }

    private unsafe static IntPtr DelegateClass()
    {
        if (s_delegateClass == IntPtr.Zero)
        {
            delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void> callback = &OnSampleBuffer;
            s_delegateClass = ObjectiveC.DefineClass(
                "ArsStudioCameraDelegate",
                "NSObject",
                "captureOutput:didOutputSampleBuffer:fromConnection:",
                (IntPtr)callback,
                "v@:@@@");
        }

        return s_delegateClass;
    }

    private unsafe static IntPtr AccessBlock()
    {
        if (s_accessBlock == IntPtr.Zero)
        {
            var descriptor = (BlockDescriptor*)NativeMemory.AllocZeroed((nuint)sizeof(BlockDescriptor));
            descriptor->Size = (ulong)sizeof(BlockLiteral);

            delegate* unmanaged<IntPtr, byte, void> invoke = &OnAccessResult;
            var block = (BlockLiteral*)NativeMemory.AllocZeroed((nuint)sizeof(BlockLiteral));
            block->Isa = ObjectiveC.Symbol(SystemLibrary, "_NSConcreteGlobalBlock");
            block->Flags = BlockIsGlobal;
            block->Invoke = (IntPtr)invoke;
            block->Descriptor = descriptor;
            s_accessBlock = (IntPtr)block;
        }

        return s_accessBlock;
    }

    [UnmanagedCallersOnly]
    private static void OnAccessResult(IntPtr block, byte granted) => s_access?.TrySetResult(granted != 0);

    [UnmanagedCallersOnly]
    private unsafe static void OnSampleBuffer(IntPtr self, IntPtr selector, IntPtr output, IntPtr sampleBuffer, IntPtr connection)
    {
        var frameArrived = s_frameArrived;
        if (frameArrived is null)
        {
            return;
        }

        var image = CMSampleBufferGetImageBuffer(sampleBuffer);
        if (image == IntPtr.Zero || CVPixelBufferLockBaseAddress(image, ReadOnlyLock) != 0)
        {
            return;
        }

        try
        {
            var width = (int)CVPixelBufferGetWidth(image);
            var height = (int)CVPixelBufferGetHeight(image);
            var stride = (int)CVPixelBufferGetBytesPerRow(image);
            var data = new ReadOnlySpan<byte>((void*)CVPixelBufferGetBaseAddress(image), stride * height);
            frameArrived(FrameImage.FromBgra(data, width, height, stride, MaxFrameWidth));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
        }
        finally
        {
            CVPixelBufferUnlockBaseAddress(image, ReadOnlyLock);
        }
    }

    [LibraryImport(CoreMedia)]
    private static partial IntPtr CMSampleBufferGetImageBuffer(IntPtr sampleBuffer);

    [LibraryImport(CoreVideo)]
    private static partial int CVPixelBufferLockBaseAddress(IntPtr pixelBuffer, ulong flags);

    [LibraryImport(CoreVideo)]
    private static partial int CVPixelBufferUnlockBaseAddress(IntPtr pixelBuffer, ulong flags);

    [LibraryImport(CoreVideo)]
    private static partial IntPtr CVPixelBufferGetBaseAddress(IntPtr pixelBuffer);

    [LibraryImport(CoreVideo)]
    private static partial nuint CVPixelBufferGetBytesPerRow(IntPtr pixelBuffer);

    [LibraryImport(CoreVideo)]
    private static partial nuint CVPixelBufferGetWidth(IntPtr pixelBuffer);

    [LibraryImport(CoreVideo)]
    private static partial nuint CVPixelBufferGetHeight(IntPtr pixelBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockDescriptor
    {
        public ulong Reserved;
        public ulong Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockLiteral
    {
        public IntPtr Isa;
        public int Flags;
        public int Reserved;
        public IntPtr Invoke;
        public unsafe BlockDescriptor* Descriptor;
    }
}
