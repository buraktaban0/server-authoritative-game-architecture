
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Tacmind.Core;
using Tacmind.ResourceManagement;
using UnityEngine;

namespace Tacmind.Networking
{

	public class NetworkStream : PoolObject
	{
		//            1         2        1          1           1 
		// HEADER: | rel | packet id | part | total parts | data type

		public static readonly int HEADER_SIZE = 8;

		public enum DataType : byte
		{
			None,
			UserCommand,
			Snapshot,
			Event,
			PingPong
		}

		public enum Reliability : byte
		{
			None,
			Unreliable,
			Reliable,
			Acknowledgement
		}

		public long time;

		public DataType dataType { get; set; }

		public Reliability reliability { get; set; } = Reliability.Unreliable;

		public ushort PacketId { get; set; }

		public byte Part { get; set; } = 0;
		public byte TotalParts { get; set; } = 0;

		public ulong Header { get; private set; }

		public bool IsReliable { get { return reliability == Reliability.Reliable; } }

		public IPEndPoint RemoteEP { get; set; } // For outgoing queue in NetworkClient

		public byte[] Bytes { get; private set; }

		public int Size { get; set; }

		public int Capacity { get; private set; }

		public ushort Owner { get; set; }

		public uint UId { get { return (uint)PacketId << 16 | (uint)Owner; } }//((uint)PacketId << 16) | ((uint)Part << 8) | (uint)TotalParts; } }

		public float receivedAt = 0f;

		private bool poolAvailability;

		private short currentEntity;

		public bool HasAvailable
		{
			get
			{
				return Available > 0;
			}
		}

		public int Available
		{
			get
			{
				return Size - CurrentPos;
			}
		}



		public int CurrentPos { get; set; }


		public override void OnReuse()
		{
			Clear();
		}


		public NetworkStream() : this(500)
		{
		}




		public NetworkStream(int bufferSize)
		{
			Capacity = bufferSize;
			Bytes = new byte[bufferSize];
			Clear();
		}

		public void Clear()
		{
			CurrentPos = HEADER_SIZE;
			Size = 0;
			Owner = 0;
			Part = 0;
			TotalParts = 0;
			dataType = DataType.None;
		}

		//            1         2        1          1           1 
		// HEADER: | rel | packet id | part | total parts | data type

		public void EndWrite()
		{
			Bytes[0] = (byte)reliability;
			Buffer.BlockCopy(BitConverter.GetBytes(PacketId), 0, Bytes, 1, 2);
			Bytes[3] = Part;
			Bytes[4] = TotalParts;
			Bytes[5] = (byte)dataType;
			//CurrentPos = HEADER_SIZE;
		}

		public void EndReceive()
		{
			reliability = (Reliability)Bytes[0];
			PacketId = BitConverter.ToUInt16(Bytes, 1);
			Part = Bytes[3];
			TotalParts = Bytes[4];
			dataType = (DataType)Bytes[5];
		}

		public ulong GetHeader()
		{
			int pos = CurrentPos;
			CurrentPos = 0;
			ulong header = ReadULong();
			CurrentPos = pos;
			return header;
		}

		public void SetHeader(ulong header)
		{
			int pos = CurrentPos;
			CurrentPos = 0;
			WriteULong(header);
			Size -= 4;
			CurrentPos = pos;
		}

		public void EvaluateDataType()
		{
			dataType = (DataType)ReadByte();
		}

		public byte[] GetBuffer()
		{
			return Bytes;
		}


		public NetworkStream GetCopy()
		{
			var ns = new NetworkStream(Size + HEADER_SIZE);
			Buffer.BlockCopy(this.Bytes, 0, ns.Bytes, 0, ns.Bytes.Length);

			ns.Size = this.Size;

			ns.reliability = this.reliability;
			ns.PacketId = this.PacketId;
			ns.Part = this.Part;
			ns.TotalParts = this.TotalParts;
			ns.dataType = this.dataType;

			ns.RemoteEP = this.RemoteEP;

			ns.Owner = this.Owner;

			return ns;
		}

		#region Write


		public void WriteTeam(Game.Team team)
		{
			WriteByte((byte)team);
		}

		public void WriteFloat(float val)
		{
			byte[] bytes = BitConverter.GetBytes(val);
			System.Buffer.BlockCopy(bytes, 0, Bytes, CurrentPos, bytes.Length);
			CurrentPos += bytes.Length;
			Size += bytes.Length;
		}

		public void WriteFloat8(float val, float min, float max)
		{
			val -= min;
			max -= min;
			byte byteVal = (byte)(byte.MaxValue * val / max + 0.5f);
			WriteByte(byteVal);
		}

		public void WriteFloat16(float val, float min, float max)
		{
			val -= min;
			max -= min;
			ushort ushortValue = (ushort)(ushort.MaxValue * val / max + 0.5f);
			WriteUShort(ushortValue);
		}

		public void WriteInt(int val)
		{
			byte[] bytes = BitConverter.GetBytes(val);
			System.Buffer.BlockCopy(bytes, 0, Bytes, CurrentPos, bytes.Length);
			CurrentPos += bytes.Length;
			Size += bytes.Length;
		}
		public void WriteUInt(uint val)
		{
			byte[] bytes = BitConverter.GetBytes(val);
			System.Buffer.BlockCopy(bytes, 0, Bytes, CurrentPos, bytes.Length);
			CurrentPos += bytes.Length;
			Size += bytes.Length;
		}

		public void WriteULong(ulong val)
		{
			byte[] bytes = BitConverter.GetBytes(val);
			System.Buffer.BlockCopy(bytes, 0, Bytes, CurrentPos, bytes.Length);
			CurrentPos += bytes.Length;
			Size += bytes.Length;
		}

		public void WriteLong(long val)
		{
			byte[] bytes = BitConverter.GetBytes(val);
			System.Buffer.BlockCopy(bytes, 0, Bytes, CurrentPos, bytes.Length);
			CurrentPos += bytes.Length;
			Size += bytes.Length;
		}

		public void WriteShort(short val)
		{
			byte[] bytes = BitConverter.GetBytes(val);
			System.Buffer.BlockCopy(bytes, 0, Bytes, CurrentPos, bytes.Length);
			CurrentPos += bytes.Length;
			Size += bytes.Length;
		}
		public void WriteUShort(ushort val)
		{
			byte[] bytes = BitConverter.GetBytes(val);
			System.Buffer.BlockCopy(bytes, 0, Bytes, CurrentPos, bytes.Length);
			CurrentPos += bytes.Length;
			Size += bytes.Length;
		}

		public void WriteBool(bool val)
		{
			byte byteVal = (byte)(val ? 1 : 0);
			WriteByte(byteVal);
		}

		public void WriteString(string val)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(val);
			WriteShort((short)bytes.Length);
			System.Buffer.BlockCopy(bytes, 0, Bytes, CurrentPos, bytes.Length);
			CurrentPos += bytes.Length;
			Size += bytes.Length;
		}

		public void WriteVector3(Vector3 val)
		{
			WriteFloat(val.x);
			WriteFloat(val.y);
			WriteFloat(val.z);
		}

		public void WriteQuaternion(Quaternion val)
		{
			WriteFloat(val.x);
			WriteFloat(val.y);
			WriteFloat(val.z);
			WriteFloat(val.w);
		}

		public void WriteTransform(Transform val)
		{
			WriteVector3(val.position);
			WriteQuaternion(val.rotation);
			WriteVector3(val.localScale);
		}

		public void WriteByte(byte val)
		{
			Bytes[CurrentPos] = val;
			CurrentPos += 1;
			Size += 1;
		}

		public void WriteBytes(byte[] bytes)
		{
			WriteBytes(bytes, 0, bytes.Length);
		}

		public void WriteBytes(byte[] bytes, int offset, int size)
		{
			System.Buffer.BlockCopy(bytes, offset, Bytes, CurrentPos, size);
			CurrentPos += size;
			Size += size;
		}

		#endregion

		#region Read

		public Game.Team ReadTeam()
		{
			return (Game.Team)(ReadByte());
		}

		public float ReadFloat()
		{
			float val = BitConverter.ToSingle(Bytes, CurrentPos);
			CurrentPos += 4;
			return val;
		}

		public float ReadFloat8(float min, float max)
		{
			float diff = max - min;
			byte bVal = ReadByte();
			return bVal * diff / byte.MaxValue + min;
		}

		public float ReadFloat16(float min, float max)
		{
			float diff = max - min;
			ushort sVal = ReadUShort();
			return sVal * diff / ushort.MaxValue + min;
		}

		public int ReadInt()
		{
			int val = BitConverter.ToInt32(Bytes, CurrentPos);
			CurrentPos += 4;
			return val;
		}

		public uint ReadUInt()
		{
			uint val = BitConverter.ToUInt32(Bytes, CurrentPos);
			CurrentPos += 4;
			return val;
		}

		public long ReadLong()
		{
			long val = BitConverter.ToInt64(Bytes, CurrentPos);
			CurrentPos += 8;
			return val;
		}

		public ulong ReadULong()
		{
			ulong val = BitConverter.ToUInt32(Bytes, CurrentPos);
			CurrentPos += 8;
			return val;
		}

		public short ReadShort()
		{
			short val = BitConverter.ToInt16(Bytes, CurrentPos);
			CurrentPos += 2;
			return val;
		}
		public ushort ReadUShort()
		{
			ushort val = BitConverter.ToUInt16(Bytes, CurrentPos);
			CurrentPos += 2;
			return val;
		}

		public bool ReadBool()
		{
			bool val = BitConverter.ToBoolean(Bytes, CurrentPos);
			CurrentPos += 1;
			return val;
		}

		public string ReadString()
		{
			int size = ReadShort();
			string val = Encoding.UTF8.GetString(Bytes, CurrentPos, size);
			CurrentPos += size;
			return val;
		}

		public Vector3 ReadVector3()
		{
			float x = ReadFloat();
			float y = ReadFloat();
			float z = ReadFloat();
			return new Vector3(x, y, z);
		}

		public Quaternion ReadQuaternion()
		{
			float x = ReadFloat();
			float y = ReadFloat();
			float z = ReadFloat();
			float w = ReadFloat();
			return new Quaternion(x, y, z, w);
		}

		public void ReadTransform(Transform t)
		{
			t.position = ReadVector3();
			t.rotation = ReadQuaternion();
			t.localScale = ReadVector3();
		}

		public byte ReadByte()
		{
			byte val = Bytes[CurrentPos];
			CurrentPos += 1;
			return val;
		}


		public void ReadBytes(byte[] buffer)
		{
			System.Buffer.BlockCopy(this.Bytes, CurrentPos, buffer, 0, buffer.Length);
			CurrentPos += buffer.Length;
		}

		#endregion


		public string BytesToString()
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < Size + HEADER_SIZE; i++)
			{
				sb.Append(Bytes[i] + " ");
				if (i % 50 == 0 && i != 0)
					sb.Append("\n");
			}

			return sb.ToString();
		}


		public static NetworkStream JoinStreams(Dictionary<byte, NetworkStream> streams)
		{
			int count = streams.Count;
			NetworkStream firstPart = streams[1];
			int partSize = firstPart.Size;

			NetworkStream ns = new NetworkStream(partSize * count);
			Buffer.BlockCopy(firstPart.Bytes, 0, ns.Bytes, 0, NetworkStream.HEADER_SIZE);

			for (int i = 0; i < count; i++)
			{
				NetworkStream part = streams[(byte)(i + 1)];
				Buffer.BlockCopy(part.Bytes, HEADER_SIZE, ns.Bytes, HEADER_SIZE + i * partSize, part.Size);
			}

			return ns;
		}


		public List<NetworkStream> Split(int maxPacketSize)
		{
			List<NetworkStream> list = new List<NetworkStream>(8);

			int parts = Size / maxPacketSize;
			int remainder = Size % maxPacketSize;

			int totalParts = parts;
			if (remainder > 0)
				totalParts += 1;

			NetworkStream part;
			for (int i = 0; i < parts; i += 1)
			{
				part = new NetworkStream(512);
				part.reliability = reliability;
				part.dataType = dataType;
				part.PacketId = PacketId;
				part.Part = (byte)(i + 1);
				part.TotalParts = (byte)totalParts;
				part.RemoteEP = RemoteEP;
				Buffer.BlockCopy(Bytes, HEADER_SIZE + i * maxPacketSize, part.Bytes, HEADER_SIZE, maxPacketSize);

				part.Size = maxPacketSize;

				part.EndWrite();

				list.Add(part);
			}

			part = new NetworkStream(remainder + HEADER_SIZE);
			part.reliability = reliability;
			part.dataType = dataType;
			part.PacketId = PacketId;
			part.Part = (byte)(parts + 1);
			part.TotalParts = (byte)(totalParts);
			part.RemoteEP = RemoteEP;
			Buffer.BlockCopy(Bytes, HEADER_SIZE + parts * maxPacketSize, part.Bytes, HEADER_SIZE, remainder);

			part.Size = remainder;

			part.EndWrite();

			list.Add(part);

			return list;

		}

	}


	public interface INetworkObject
	{
		void ReadFrom(NetworkStream ns);
		void WriteTo(NetworkStream ns);
	}

}