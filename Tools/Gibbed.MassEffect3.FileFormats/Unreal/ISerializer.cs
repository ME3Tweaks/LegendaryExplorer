/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Gibbed.MassEffect3.FileFormats.Unreal
{
    public interface ISerializer
    {
        uint Version { get; }
        SerializeMode Mode { get; }

        void Serialize(ref bool value);
        void Serialize(ref bool value, Func<ISerializer, bool> condition, Func<bool> defaultValue);

        void Serialize(ref byte value);
        void Serialize(ref byte value, Func<ISerializer, bool> condition, Func<byte> defaultValue);

        void Serialize(ref int value);
        void Serialize(ref int value, Func<ISerializer, bool> condition, Func<int> defaultValue);

        void Serialize(ref uint value);
        void Serialize(ref uint value, Func<ISerializer, bool> condition, Func<uint> defaultValue);

        void Serialize(ref float value);
        void Serialize(ref float value, Func<ISerializer, bool> condition, Func<float> defaultValue);

        void Serialize(ref string value);
        void Serialize(ref string value, Func<ISerializer, bool> condition, Func<string> defaultValue);

        void Serialize(ref Guid value);
        void Serialize(ref Guid value, Func<ISerializer, bool> condition, Func<Guid> defaultValue);

        void SerializeEnum<TEnum>(ref TEnum value);
        void SerializeEnum<TEnum>(ref TEnum value, Func<ISerializer, bool> condition, Func<TEnum> defaultValue);

        void Serialize<TType>(ref TType value)
            where TType : class, ISerializable, new();

        void Serialize<TType>(ref TType value, Func<ISerializer, bool> condition, Func<TType> defaultValue)
            where TType : class, ISerializable, new();

        void Serialize(ref BitArray list);
        void Serialize(ref BitArray list, Func<ISerializer, bool> condition, Func<BitArray> defaultList);

        void Serialize(ref List<byte> list);
        void Serialize(ref List<byte> list, Func<ISerializer, bool> condition, Func<List<byte>> defaultList);

        void Serialize(ref List<int> list);
        void Serialize(ref List<int> list, Func<ISerializer, bool> condition, Func<List<int>> defaultList);

        void Serialize(ref List<float> list);
        void Serialize(ref List<float> list, Func<ISerializer, bool> condition, Func<List<float>> defaultList);

        void Serialize(ref List<string> list);
        void Serialize(ref List<string> list, Func<ISerializer, bool> condition, Func<List<string>> defaultList);

        void Serialize(ref List<Guid> list);
        void Serialize(ref List<Guid> list, Func<ISerializer, bool> condition, Func<List<Guid>> defaultList);

        void SerializeEnum<TEnum>(ref List<TEnum> list);
        void SerializeEnum<TEnum>(ref List<TEnum> list, Func<ISerializer, bool> condition, Func<List<TEnum>> defaultList);

        void Serialize<TType>(ref List<TType> list)
            where TType : class, ISerializable, new();

        void Serialize<TType>(ref List<TType> list, Func<ISerializer, bool> condition, Func<List<TType>> defaultList)
            where TType : class, ISerializable, new();

        void Serialize<TType>(ref BindingList<TType> list)
            where TType : class, ISerializable, new();

        void Serialize<TType>(ref BindingList<TType> list,
                              Func<ISerializer, bool> condition,
                              Func<BindingList<TType>> defaultList)
            where TType : class, ISerializable, new();
    }
}
