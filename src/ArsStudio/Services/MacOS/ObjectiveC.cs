using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ArsStudio.Services.MacOS;

[SupportedOSPlatform("macos")]
internal static unsafe partial class ObjectiveC
{
    private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";
    private const string SystemLibrary = "/usr/lib/libSystem.B.dylib";

    private static readonly IntPtr MessageSend = NativeLibrary.GetExport(NativeLibrary.Load(ObjCLibrary), "objc_msgSend");

    public static IntPtr Class(string name) => objc_getClass(name);

    public static IntPtr Selector(string name) => sel_registerName(name);

    public static IntPtr Send(IntPtr target, string selector) =>
        ((delegate* unmanaged<IntPtr, IntPtr, IntPtr>)MessageSend)(target, Selector(selector));

    public static IntPtr Send(IntPtr target, string selector, IntPtr first) =>
        ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr>)MessageSend)(target, Selector(selector), first);

    public static IntPtr Send(IntPtr target, string selector, IntPtr first, IntPtr second) =>
        ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>)MessageSend)(target, Selector(selector), first, second);

    public static IntPtr Send(IntPtr target, string selector, IntPtr first, IntPtr second, IntPtr third) =>
        ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>)MessageSend)(target, Selector(selector), first, second, third);

    public static bool SendBool(IntPtr target, string selector, IntPtr argument) =>
        ((delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte>)MessageSend)(target, Selector(selector), argument) != 0;

    public static void SendFlag(IntPtr target, string selector, bool value) =>
        ((delegate* unmanaged<IntPtr, IntPtr, byte, void>)MessageSend)(target, Selector(selector), value ? (byte)1 : (byte)0);

    public static IntPtr Retain(IntPtr target) => target == IntPtr.Zero ? target : Send(target, "retain");

    public static void Release(IntPtr target)
    {
        if (target != IntPtr.Zero)
        {
            Send(target, "release");
        }
    }

    public static IntPtr String(string value)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return Send(Class("NSString"), "stringWithUTF8String:", utf8);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
        }
    }

    public static string? ToManaged(IntPtr nsString) =>
        nsString == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Send(nsString, "UTF8String"));

    public static IntPtr Constant(string framework, string symbol)
    {
        var library = NativeLibrary.Load(framework);
        return NativeLibrary.TryGetExport(library, symbol, out var address) ? *(IntPtr*)address : IntPtr.Zero;
    }

    public static IntPtr Symbol(string library, string symbol) =>
        NativeLibrary.GetExport(NativeLibrary.Load(library), symbol);

    public static IntPtr PushPool() => objc_autoreleasePoolPush();

    public static void PopPool(IntPtr pool) => objc_autoreleasePoolPop(pool);

    public static IntPtr CreateQueue(string label)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(label);
        try
        {
            return dispatch_queue_create(utf8, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
        }
    }

    public static IntPtr DefineClass(string name, string superclass, string selector, IntPtr implementation, string types)
    {
        var existing = objc_getClass(name);
        if (existing != IntPtr.Zero)
        {
            return existing;
        }

        var type = objc_allocateClassPair(objc_getClass(superclass), name, 0);
        class_addMethod(type, Selector(selector), implementation, types);
        objc_registerClassPair(type);
        return type;
    }

    [LibraryImport(ObjCLibrary, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_getClass(string name);

    [LibraryImport(ObjCLibrary, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string name);

    [LibraryImport(ObjCLibrary, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_allocateClassPair(IntPtr superclass, string name, nint extraBytes);

    [LibraryImport(ObjCLibrary)]
    private static partial void objc_registerClassPair(IntPtr type);

    [LibraryImport(ObjCLibrary, StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool class_addMethod(IntPtr type, IntPtr selector, IntPtr implementation, string types);

    [LibraryImport(ObjCLibrary)]
    private static partial IntPtr objc_autoreleasePoolPush();

    [LibraryImport(ObjCLibrary)]
    private static partial void objc_autoreleasePoolPop(IntPtr pool);

    [LibraryImport(SystemLibrary)]
    private static partial IntPtr dispatch_queue_create(IntPtr label, IntPtr attributes);
}
