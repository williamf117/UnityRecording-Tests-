using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UltimateReplay.Util;
using UltimateReplay.Core;

namespace UltimateReplay
{
    /// <summary>
    /// A <see cref="ReplayState"/> allows replay objects to serialize and deserialize their data.
    /// See <see cref="IReplaySerialize"/>. 
    /// </summary>
    public sealed class ReplayState
    {
        // Private
        private const int maxByteAllocation = 4; // Dont allow 64 bit types

        private static byte[] sharedBuffer = new byte[maxByteAllocation];
        private List<byte> bytes = new List<byte>();
        private int readPointer = 0;

        // Properties
        /// <summary>
        /// Returns true if the state contains any more data.
        /// </summary>
        public bool CanRead
        {
            get { return bytes.Count > 0; }
        }

        /// <summary>
        /// Returns true if the read pointer is at the end of the buffered data or false if there is still data to be read.
        /// </summary>
        public bool EndRead
        {
            get { return readPointer >= Size; }
        }

        /// <summary>
        /// Returns the size of the object state in bytes.
        /// </summary>
        public int Size
        {
            get { return bytes.Count; }
        }

        // Constructor
        /// <summary>
        /// Create an empty <see cref="ReplayState"/> that can be written to. 
        /// </summary>
        public ReplayState() { }

        internal ReplayState(byte[] data)
        {
            // Add all bytes to the state
            bytes.AddRange(data);
        }

        // Methods
        internal void PrepareForRead()
        {
            // Reset the read pointer
            readPointer = 0;
        }

        /// <summary>
        /// Clears all buffered data from this <see cref="ReplayState"/> and resets its state.
        /// </summary>
        public void Clear()
        {
            bytes.Clear();
            readPointer = 0;
        }

        /// <summary>
        /// Get the <see cref="ReplayState"/> data as a byte array. 
        /// </summary>
        /// <returns>A byte array of data</returns>
        public byte[] ToArray()
        {
            // Convert to byte array
            return bytes.ToArray();
        }

        /// <summary>
        /// Write a byte to the state.
        /// </summary>
        /// <param name="value">Byte value</param>
        public void Write(byte value)
        {
            bytes.Add(value);
        }

        /// <summary>
        /// Write a byte array to the state.
        /// </summary>
        /// <param name="bytes">Byte array value</param>
        public void Write(byte[] bytes)
        {
            for(int i = 0; i < bytes.Length; i++)
                Write(bytes[i]);
        }

        /// <summary>
        /// Write a byte array to the state using an offset position and length.
        /// </summary>
        /// <param name="bytes">Byte array value</param>
        /// <param name="offset">The start index to read data from the array</param>
        /// <param name="length">The amount of data to read</param>
        public void Write(byte[] bytes, int offset, int length)
        {
            for (int i = offset; i < length; i++)
                Write(bytes[i]);
        }

        /// <summary>
        /// Write a short to the state.
        /// </summary>
        /// <param name="value">Short value</param>
        public void Write(short value)
        {
            // Use the shared buffer instead of allocating a new array
            BitConverterNonAlloc.GetBytes(sharedBuffer, value);

            // Write all bytes
            Write(sharedBuffer, 0, sizeof(short));
        }

        /// <summary>
        /// Write an int to the state.
        /// </summary>
        /// <param name="value">Int value</param>
        public void Write(int value)
        {
            // Use the shared buffer instead of allocating a new array
            BitConverterNonAlloc.GetBytes(sharedBuffer, value);

            // Write all bytes
            Write(sharedBuffer, 0, sizeof(int));
        }

        /// <summary>
        /// Write a float to the state.
        /// </summary>
        /// <param name="value">Float value</param>
        public void Write(float value)
        {
            // Use the shared buffer instead of allocating a new array
            BitConverterNonAlloc.GetBytes(sharedBuffer, value);

            // Write all bytes
            Write(sharedBuffer, 0, sizeof(float));
        }

        /// <summary>
        /// Write a bool to the state.
        /// </summary>
        /// <param name="value">bool value</param>
        public void Write(bool value)
        {
            // Use the shared buffer instead of allocating a new array
            BitConverterNonAlloc.GetBytes(sharedBuffer, value);

            // Write all bytes
            Write(sharedBuffer, 0, sizeof(bool));
        }

        /// <summary>
        /// Write a string to the state.
        /// </summary>
        /// <param name="value">string value</param>
        public void Write(string value)
        {
            // Get string bytes
#if UNITY_WINRT && !UNITY_EDITOR
            byte[] bytes = Encoding.UTF8.GetBytes(value);
#else
            byte[] bytes = Encoding.Default.GetBytes(value);
#endif

            // Write all bytes
            Write((short)bytes.Length);
            Write(bytes);
        }


        /// <summary>
        /// Write the specified replay identity to this <see cref="ReplayState"/>. 
        /// </summary>
        /// <param name="identity">The identity to write</param>
        public void Write(ReplayIdentity identity)
        {
            if (ReplayIdentity.byteSize == 4)
            {
                // Write as 4 byte id
                Write((int)identity);
            }
            else
            {
                // Write as 2 byte id
                Write((short)identity);
            }
        }

        /// <summary>
        /// Write the entire contents of a <see cref="ReplayState"/> to this <see cref="ReplayState"/>.
        /// All bytes will be appended.
        /// </summary>
        /// <param name="other">The other state to append</param>
        public void Write(ReplayState other)
        {
            // Copy all bytes
            foreach (byte value in other.bytes)
                Write(value);
        }

        /// <summary>
        /// Write a vector2 to the state.
        /// </summary>
        /// <param name="value">Vector2 value</param>
        public void Write(Vector2 value)
        {
            Write(value.x);
            Write(value.y);
        }

        /// <summary>
        /// Write a vector3 to the state.
        /// </summary>
        /// <param name="value">Vector3 value</param>
        public void Write(Vector3 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
        }

        /// <summary>
        /// Write a vector4 to the state.
        /// </summary>
        /// <param name="value">Vector4 value</param>
        public void Write(Vector4 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        /// <summary>
        /// Write a quaternion to the state.
        /// </summary>
        /// <param name="value">Quaternion value</param>
        public void Write(Quaternion value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        /// <summary>
        /// Write a colour to the state.
        /// </summary>
        /// <param name="value">Colour value</param>
        public void Write(Color value)
        {
            Write(value.r);
            Write(value.g);
            Write(value.b);
            Write(value.a);
        }

        /// <summary>
        /// Write a colour32 value to the state.
        /// </summary>
        /// <param name="value">Colour32 value</param>
        public void Write(Color32 value)
        {
            Write(value.r);
            Write(value.g);
            Write(value.b);
            Write(value.a);
        }

        /// <summary>
        /// Attempts to write a 32 bit float value as a low precision 16 bit representation.
        /// You should only use this method when the value is relativley small (less than 65000).
        /// Accuracy may be lost by storing low precision values.
        /// </summary>
        /// <param name="value">float value</param>
        public void WriteLowPrecision(float value)
        {
            //int count = 0;

            //while(value != Math.Floor(value))
            //{
            //    // Shift left
            //    value *= 10f;
            //    count++;
            //}

            //// Encode the value into 2 bytes
            //short encoded = (short)((count << 12) + (int)value);

            short encoded = (short)(value * 256);

            // Write the short value
            Write(encoded);
        }

        /// <summary>
        /// Write a vector2 to the state using half precision packing.
        /// Accuracy may be lost by storing low precision values.
        /// </summary>
        /// <param name="value">vector2 value</param>
        public void WriteLowPrecision(Vector2 value)
        {
            WriteLowPrecision(value.x);
            WriteLowPrecision(value.y);
        }

        /// <summary>
        /// Write a vector3 to the state using half precision packing.
        /// Accuracy may be lost by storing low precision values.
        /// </summary>
        /// <param name="value">vector3 value</param>
        public void WriteLowPrecision(Vector3 value)
        {
            WriteLowPrecision(value.x);
            WriteLowPrecision(value.y);
            WriteLowPrecision(value.z);
        }

        /// <summary>
        /// Write a vector4 to the state using half precision packing.
        /// Accuracy may be lost by storing low precision values.
        /// </summary>
        /// <param name="value">vector4 value</param>
        public void WriteLowPrecision(Vector4 value)
        {
            WriteLowPrecision(value.x);
            WriteLowPrecision(value.y);
            WriteLowPrecision(value.z);
            WriteLowPrecision(value.w);
        }

        /// <summary>
        /// Write a quaternion to the state using half precision packing.
        /// Accuracy may be lost by storing low precision values.
        /// </summary>
        /// <param name="value">quaternion value</param>
        public void WriteLowPrecision(Quaternion value)
        {
            WriteLowPrecision(value.x);
            WriteLowPrecision(value.y);
            WriteLowPrecision(value.z);
            WriteLowPrecision(value.w);
        }

        /// <summary>
        /// Attempts to read an object state from this <see cref="ReplayState"/>. 
        /// </summary>
        /// <returns>The state data for the object</returns>
        public object TryReadObject()
        {
            string typeName = ReadString();

            // Decode the type
            Type type = Type.GetType(typeName);

            // Make sure we have a valid type
            if (type == null)
                throw new InvalidOperationException("Attempted to read an object from the state but its type information could not be decoded");

            // Check for replay serialize
#if UNITY_WINRT && !UNITY_EDITOR
            if(typeof(IReplaySerialize).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()) == true)
#else
            if (typeof(IReplaySerialize).IsAssignableFrom(type) == true)
#endif
            {
                // Try to create an instance of the serializer
                IReplaySerialize serializer = (IReplaySerialize)Activator.CreateInstance(type);

                // Deserialize the object
                serializer.OnReplayDeserialize(this);

                // Return the serializer object
                return serializer;
            }
            else
            {
                object result;

                // Try to find a deserializer for the object type
                bool matched = TypeSwitchReturn(type, out result, 
                    TypeCaseReturn<byte>(ReadByte),
                    TypeCaseReturn<short>(Read16),
                    TypeCaseReturn<int>(Read32),
                    TypeCaseReturn<float>(ReadFloat),
                    TypeCaseReturn<bool>(ReadBool),
                    TypeCaseReturn<string>(ReadString),
                    TypeCaseReturn<Vector2>(ReadVec2),
                    TypeCaseReturn<Vector3>(ReadVec3),
                    TypeCaseReturn<Vector4>(ReadVec4),
                    TypeCaseReturn<Quaternion>(ReadQuat),
                    TypeCaseReturn<Color>(ReadColor),
                    TypeCaseReturn<Color32>(ReadColor32)
                    );

                // Check if we matched the type
                if (matched == false)
                {
                    // Failure
                    throw new NotSupportedException(string.Format("There is no deserializer for type '{0}'. Try implementing 'IReplaySerialize' to ensure the type can be deserialized correctly", type));
                }

                // Get the object result
                return result;
            }
        }

        /// <summary>
        /// Attempts to write an object to this <see cref="ReplayState"/>. 
        /// This method may write extra meta data for deserialization purposes which may cause excessive storage size.
        /// Use one of the <see cref="Write(byte)"/> methods if the type is known at compile time. 
        /// </summary>
        /// <param name="value">The object to write to the state</param>
        public void TryWriteObject(object value)
        {
            // Get the object type
            Type valueType = value.GetType();

            // Check if the type is name resolvable (mscorlib or Assembly-CSharp)
#if UNITY_WINRT && !UNITY_EDITOR
            if(valueType.GetTypeInfo().Assembly == typeof(Type).GetTypeInfo().Assembly || valueType.GetTypeInfo().Assembly == typeof(ReplayState).GetTypeInfo().Assembly)
#else
            if (valueType.Assembly == typeof(Type).Assembly || valueType.Assembly == typeof(ReplayState).Assembly)
#endif
            {
                // We can get away with the namespace qualified name
                Write(valueType.FullName);
            }
            else
            {
                // The type is defined in some other assembly so we need the full assembly qualified string (Many bytes!!!)
                Write(valueType.AssemblyQualifiedName);
            }

            // Check for replay serialize
#if UNITY_WINRT && !UNITY_EDITOR
            if(typeof(IReplaySerialize).GetTypeInfo().IsAssignableFrom(valueType.GetTypeInfo()) == true)
#else
            if (typeof(IReplaySerialize).IsAssignableFrom(valueType) == true)
#endif
            {
                // The type implements its own serialization
                (value as IReplaySerialize).OnReplaySerialize(this);
            }
            else
            {
                // Try to find a type seriaizer for the object
                bool matched = TypeSwitch(valueType, value,
                    TypeCase<byte>(Write),
                    TypeCase<short>(Write),
                    TypeCase<int>(Write),
                    TypeCase<float>(Write),
                    TypeCase<bool>(Write),
                    TypeCase<string>(Write),
                    TypeCase<Vector2>(Write),
                    TypeCase<Vector3>(Write),
                    TypeCase<Vector4>(Write),
                    TypeCase<Quaternion>(Write),
                    TypeCase<Color>(Write),
                    TypeCase<Color32>(Write)
                    );

                // Check if we matched the type
                if (matched == false)
                {
                    // Failure
                    throw new NotSupportedException(string.Format("There is no serializer for type '{0}'. Try implementing 'IReplaySerialize' to ensure the type can be seriaized correctly", valueType));
                }
            }
        }

        /// <summary>
        /// Read a byte from the state.
        /// </summary>
        /// <returns>Byte value</returns>
        public byte ReadByte()
        {
            if (CanRead == false)
                throw new InvalidOperationException("There is no data in the object state");

            // Check for incorrect bytes
            if (readPointer >= bytes.Count)
                throw new InvalidOperationException("There are not enough bytes in the data to read the specified type");

            byte value = bytes[readPointer];

            // Advance pointer
            readPointer++;

            return value;
        }

        /// <summary>
        /// Read a byte array from the state.
        /// </summary>
        /// <param name="amount">The number of bytes to read</param>
        /// <returns>Byte array value</returns>
        public byte[] ReadBytes(int amount)
        {
            byte[] bytes = new byte[amount];

            // Store bytes
            for (int i = 0; i < amount; i++)
                bytes[i] = ReadByte();

            return bytes;
        }

        /// <summary>
        /// Fill a byte array with data from the state.
        /// </summary>
        /// <param name="buffer">The byte array to store data in</param>
        /// <param name="offset">The index offset to start filling the buffer at</param>
        /// <param name="amount">The number of bytes to read</param>
        public void ReadBytes(byte[] buffer, int offset, int amount)
        {
            for (int i = offset; i < amount; i++)
                buffer[i] = ReadByte();
        }

        /// <summary>
        /// Read a short from the state.
        /// </summary>
        /// <returns>Short value</returns>
        public short Read16()
        {
            // Read into the shared buffer
            ReadBytes(sharedBuffer, 0, sizeof(short));

            // Convert to short
            return BitConverterNonAlloc.GetShort(sharedBuffer);
        }

        /// <summary>
        /// Read an int from the state.
        /// </summary>
        /// <returns>Int value</returns>
        public int Read32()
        {
            // Read into the shared buffer
            ReadBytes(sharedBuffer, 0, sizeof(int));

            // Convert to int
            return BitConverterNonAlloc.GetInt(sharedBuffer);
        }

        /// <summary>
        /// Read a float from the state.
        /// </summary>
        /// <returns>Float value</returns>
        public float ReadFloat()
        {
            // Read into the shared buffer
            ReadBytes(sharedBuffer, 0, sizeof(float));

            // Convert to float
            return BitConverterNonAlloc.GetFloat(sharedBuffer);
        }

        /// <summary>
        /// Read a bool from the state.
        /// </summary>
        /// <returns>Bool value</returns>
        public bool ReadBool()
        {
            // Read into the shared buffer
            ReadBytes(sharedBuffer, 0, sizeof(bool));

            // Convert to bool
            return BitConverterNonAlloc.GetBool(sharedBuffer);
        }

        /// <summary>
        /// Read a string from the state
        /// </summary>
        /// <returns>string value</returns>
        public string ReadString()
        {
            // Read the string size
            short size = Read16();

            // Read the required number of bytes
            byte[] bytes = ReadBytes(size);

            // Decode the string
#if UNITY_WINRT && !UNITY_EDITOR
            return Encoding.UTF8.GetString(bytes);
#else
            return Encoding.Default.GetString(bytes);
#endif
        }

        /// <summary>
        /// Read a <see cref="ReplayIdentity"/> from the state. 
        /// </summary>
        /// <returns>Identity value</returns>
        public ReplayIdentity ReadIdentity()
        {
            if (ReplayIdentity.byteSize == 4)
            {
                // Read as 4 byte value
                return new ReplayIdentity(Read32());
            }
            else
            {
                // Read as 2 byte value
                return new ReplayIdentity(Read16());
            }
        }

        /// <summary>
        /// Read the specified amount of bytes as a new <see cref="ReplayState"/>. 
        /// </summary>
        /// <param name="bytes">The amount of bytes to read into the state</param>
        /// <returns>A new <see cref="ReplayState"/> containing the specified number of bytes</returns>
        public ReplayState ReadState(int bytes)
        {
            // Read the required amount of bytes
            byte[] data = ReadBytes(bytes);

            // Create the state
            return new ReplayState(data);
        }

        /// <summary>
        /// Read a vector2 from the state.
        /// </summary>
        /// <returns>Vector2 value</returns>
        public Vector2 ReadVec2()
        {
            float x = ReadFloat();
            float y = ReadFloat();

            // Create vector
            return new Vector2(x, y);
        }

        /// <summary>
        /// Read a vector3 from the state.
        /// </summary>
        /// <returns>Vector3 value</returns>
        public Vector3 ReadVec3()
        {
            float x = ReadFloat();
            float y = ReadFloat();
            float z = ReadFloat();

            // Create vector
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Read a vector4 from the state.
        /// </summary>
        /// <returns>Vector4 value</returns>
        public Vector4 ReadVec4()
        {
            float x = ReadFloat();
            float y = ReadFloat();
            float z = ReadFloat();
            float w = ReadFloat();

            // Create vector
            return new Vector4(x, y, z, w);
        }

        /// <summary>
        /// Read a quaternion from the state.
        /// </summary>
        /// <returns>Quaternion value</returns>
        public Quaternion ReadQuat()
        {
            float x = ReadFloat();
            float y = ReadFloat();
            float z = ReadFloat();
            float w = ReadFloat();

            // Create quaternion
            return new Quaternion(x, y, z, w);
        }

        /// <summary>
        /// Read a colour from the state.
        /// </summary>
        /// <returns>Colour value</returns>
        public Color ReadColor()
        {
            float r = ReadFloat();
            float g = ReadFloat();
            float b = ReadFloat();
            float a = ReadFloat();

            // Create colour
            return new Color(r, g, b, a);
        }

        /// <summary>
        /// Read a colour32 from the state.
        /// </summary>
        /// <returns>Colour32 value</returns>
        public Color32 ReadColor32()
        {
            byte r = ReadByte();
            byte g = ReadByte();
            byte b = ReadByte();
            byte a = ReadByte();

            // Create colour
            return new Color32(r, g, b, a);
        }

        /// <summary>
        /// Attempts to read a low precision float.
        /// You should only use this method when the value is relativley small (less than 65000) and accuracy is not essential.
        /// </summary>
        /// <returns>float value</returns>
        public float ReadFloatLowPrecision()
        {
            // Read 16 bits
            short value = Read16();

            //// Find the factor
            //int count = value >> 12;

            //// Decode main
            //float decoded = value & 0xfff;

            //while(count > 0)
            //{
            //    decoded /= 10f;
            //    count--;
            //}

            float decoded = value / 256f;

            return decoded;
        }

        /// <summary>
        /// Attempts to read a low precision vector2.
        /// </summary>
        /// <returns>vector2 value</returns>
        public Vector2 ReadVec2LowPrecision()
        {
            float x = ReadFloatLowPrecision();
            float y = ReadFloatLowPrecision();

            // Create vector
            return new Vector2(x, y);
        }

        /// <summary>
        /// Attempts to read a low precision vector3.
        /// </summary>
        /// <returns>vector3 value</returns>
        public Vector3 ReadVec3LowPrecision()
        {
            float x = ReadFloatLowPrecision();
            float y = ReadFloatLowPrecision();
            float z = ReadFloatLowPrecision();

            // Create vector
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Attempts to read a low precision vector4.
        /// </summary>
        /// <returns>vector4 value</returns>
        public Vector4 ReadVec4LowPrecision()
        {
            float x = ReadFloatLowPrecision();
            float y = ReadFloatLowPrecision();
            float z = ReadFloatLowPrecision();
            float w = ReadFloatLowPrecision();

            // Create vector
            return new Vector4(x, y, z, w);
        }

        /// <summary>
        /// Attempts to read a low precision quaternion.
        /// </summary>
        /// <returns>quaternion value</returns>
        public Quaternion ReadQuatLowPrecision()
        {
            float x = ReadFloatLowPrecision();
            float y = ReadFloatLowPrecision();
            float z = ReadFloatLowPrecision();
            float w = ReadFloatLowPrecision();

            // Create vector
            return new Quaternion(x, y, z, w);
        }

        public void WriteToBinary(BinaryWriter writer)
        {
            writer.Write(Size);

            // Write without modifying state
            for (int i = 0; i < Size; i++)
                writer.Write(bytes[i]);
        }

        public void ReadFromBinary(BinaryReader reader)
        {
            int size = reader.ReadInt32();

            // Read all bytes
            byte[] data = reader.ReadBytes(size);

            // Recreate storage list
            bytes = new List<byte>(data);

            // Make sure the read state is reset so that the read index is valid
            PrepareForRead();
        }

        private static bool TypeSwitch(Type type,  object value, params KeyValuePair<Type, Action<object>>[] checkers)
        {
            foreach(KeyValuePair<Type, Action<object>> pair in checkers)
            {
                // Check for matching type
                if(type == pair.Key)
                {
                    // Call the method
                    pair.Value(value);
                    return true;
                }
            }

            // No match found
            return false;
        }

        private static bool TypeSwitchReturn(Type type, out object result, params KeyValuePair<Type, Func<object>>[] checkers)
        {
            foreach(KeyValuePair<Type, Func<object>> pair in checkers)
            {
                // Check for matching type
                if(type == pair.Key)
                {
                    // Call the method
                    result = pair.Value();
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static KeyValuePair<Type, Action<object>> TypeCase<T>(Action<T> match)
        {
            // Create a key value pair
            return new KeyValuePair<Type, Action<object>>(typeof(T), (object o) =>
            {
                // Try cast
                match((T)o);
            });
        }

        private static KeyValuePair<Type, Func<object>> TypeCaseReturn<T>(Func<T> match)
        {
            return new KeyValuePair<Type, Func<object>>(typeof(T), () =>
            {
                return match();
            });
        }
    }
}
