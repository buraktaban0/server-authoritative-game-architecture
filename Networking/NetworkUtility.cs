using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Tacmind.Networking
{

	public static class NetworkUtility
	{

		public static long StartTime = GetUnixTimestamp();

		public static float Time
		{
			get
			{
				return (GetUnixTimestamp() - StartTime) * 0.001f;
			}
		}

		private static readonly string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"; // + "+-*?";

		private static readonly System.Random rand = new System.Random();

		public static string GetRandomString(int length)
		{
			var stringChars = new char[length];

			for (int i = 0; i < length; i++)
			{
				stringChars[i] = chars[rand.Next(chars.Length)];
			}

			var finalString = new string(stringChars);

			return finalString;
		}

		public static bool WrapGreater(ushort a, ushort b)
		{
			ushort max = ushort.MaxValue;
			ushort half = (ushort)(max / 2);

			int diff = a - b;

			if (diff >= half)
			{
				return false;
			}
			else if (diff <= -half)
			{
				return true;
			}
			else
			{
				return a > b;
			}
		}

		public static bool WrapGreaterEqual(ushort a, ushort b)
		{
			ushort max = ushort.MaxValue;
			ushort half = (ushort)(max / 2);

			int diff = a - b;

			if (diff >= half)
			{
				return false;
			}
			else if (diff <= -half)
			{
				return true;
			}
			else
			{
				return a >= b;
			}
		}

		public static long GetUnixTimestamp()
		{
			return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		}

		public static IPEndPoint ParseIPEndPoint(string s)
		{
			string[] split = s.Split(':');
			return new IPEndPoint(IPAddress.Parse(split[0]), int.Parse(split[1]));
		}

		public static bool IsLarger(short a, short b)
		{
			short mid = short.MaxValue / 2;
			short dif = (short)(b - a);

			if (dif >= mid)
			{
				return true;
			}

			return a > b;
		}

		public static byte Pack(int i1, int i2)
		{
			return (byte)((i1 << 4) + i2);
		}
		public static int[] Unpack2(byte b)
		{
			int[] i = new int[2];
			i[0] = b >> 4;
			i[1] = b & 15;
			return i;
		}

		public static byte Pack(int i1, int i2, int i3, int i4 = 0)
		{
			return (byte)((i1 << 6) | (i2 << 4) | (i3 << 2) | i4);
		}
		public static int[] Unpack4(byte b)
		{
			int[] i = new int[4];
			i[0] = (b & 192) >> 6;
			i[1] = (b & 48) >> 4;
			i[2] = (b & 12) >> 2;
			i[3] = (b & 3);
			return i;
		}

		public static byte Pack(int i1, int i2, int i3, int i4, int i5, int i6 = 0, int i7 = 0, int i8 = 0)
		{
			return (byte)((i1 << 7) | (i2 << 6) | (i3 << 5) | (i4 << 4) | (i5 << 3) | (i6 << 2) | (i7 << 1) | (i8));
		}
		public static int[] Unpack8(byte b)
		{
			int[] i = new int[8];

			i[0] = (b & 128) >> 7;
			i[1] = (b & 64) >> 6;
			i[2] = (b & 32) >> 5;
			i[3] = (b & 16) >> 4;
			i[4] = (b & 8) >> 3;
			i[5] = (b & 4) >> 2;
			i[6] = (b & 2) >> 1;
			i[7] = (b & 1);
			return i;
		}

		public static int SetByte(int val, int index, byte to)
		{
			index *= 8;
			int byteMax = ~(255 << index);
			val = val & byteMax;
			val = val | (to << index);
			return val;
		}
		public static ulong SetByte(ulong val, int index, byte to)
		{
			index *= 8;
			ulong byteMax = (~(255u << index));
			val = val & byteMax;
			val = val | ((ulong)to << index);
			return val;
		}


		public static byte DeltaEncode(float newF, float oldF)
		{
			float diff = newF - oldF;
			float rem = diff % 0.01f;

			int result = (int)(diff / 0.01f);

			if (diff < 0)
			{
				if (rem < -0.005f)
				{
					result -= 1;
				}
			}
			else
			{
				if (rem > 0.005f)
				{
					result += 1;
				}
			}

			return (byte)(Mathf.Clamp(result, -128, 127) + 128);
		}

		public static float DeltaDecode(float oldF, byte delta)
		{
			int del = delta - 128;
			return oldF + del * 0.01f;
		}


		public static bool IsFloatInvalid(float f)
		{
			return float.IsNaN(f) || float.IsInfinity(f);
		}

		public static bool IsVector3Invalid(ref Vector3 vec)
		{
			return IsFloatInvalid(vec.x) || IsFloatInvalid(vec.y) || IsFloatInvalid(vec.z);
		}
		public static bool IsQuaterniontInvalid(ref Quaternion rot)
		{
			return IsFloatInvalid(rot.x) || IsFloatInvalid(rot.y) || IsFloatInvalid(rot.z) || IsFloatInvalid(rot.w);
		}
	}

}

