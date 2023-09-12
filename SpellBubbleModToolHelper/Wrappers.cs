using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SpellBubbleModToolHelper;

public partial class BridgeLib
{
    private static DualArrayWrapper PackWrappers(ArrayWrapper wrapper1, ArrayWrapper wrapper2)
    {
        var dualWrapper = new DualArrayWrapper
        {
            size = wrapper1.size,
            array = wrapper1.array,
            size2 = wrapper2.size,
            array2 = wrapper2.array
        };
        return dualWrapper;
    }

    private static int[] WrapperToArray_int(ArrayWrapper wrapper)
    {
        var array = new int[wrapper.size];
        Marshal.Copy(wrapper.array, array, 0, (int) wrapper.size);
        return array;
    }

    private static ArrayWrapper ArrayToWrapper_IntPtr(IntPtr[] array)
    {
        var arrayPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<IntPtr>() * array.Length);
        Marshal.Copy(array, 0, arrayPointer, array.Length);
        var wrapper = new ArrayWrapper
        {
            size = (uint) array.Length,
            array = arrayPointer
        };
        return wrapper;
    }

    private static IEnumerable<IntPtr> WrapperToArray_IntPtr(ArrayWrapper wrapper)
    {
        var array = new IntPtr[wrapper.size];
        Marshal.Copy(wrapper.array, array, 0, (int) wrapper.size);
        return array;
    }

    private static T[] WrapperToArray_Struct<T>(ArrayWrapper wrapper)
    {
        var array = new T[wrapper.size];
        var size = Marshal.SizeOf<T>();

        for (var i = 0; i < wrapper.size; ++i)
        {
            var element = new IntPtr(wrapper.array.ToInt64() + i * size);
            array[i] = Marshal.PtrToStructure<T>(element);
        }

        return array;
    }

    private static ArrayWrapper ArrayToWrapper_Struct<T>(T[] array)
    {
        var arrayPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<T>() * array.Length);

        for (var i = 0; i < array.Length; ++i)
        {
            var obj = array[i];
            Marshal.StructureToPtr(obj, arrayPointer + i * Marshal.SizeOf<T>(), true);
        }

        var wrapper = new ArrayWrapper
        {
            size = (uint) array.Length,
            array = arrayPointer
        };
        return wrapper;
    }

    private static ArrayWrapper ArrayToWrapper_int(int[] array)
    {
        var arrayPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<uint>() * array.Length);
        Marshal.Copy(array, 0, arrayPointer, array.Length);
        var wrapper = new ArrayWrapper
        {
            size = (uint) array.Length,
            array = arrayPointer
        };
        return wrapper;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ArrayWrapper
    {
        public uint managed = 1;
        public uint size;
        public IntPtr array; // The type of array element is defined as "usize" in Rust

        public ArrayWrapper()
        {
            size = 0;
            array = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DualArrayWrapper
    {
        public uint size;
        public IntPtr array;
        public uint size2;
        public IntPtr array2;
    }
}