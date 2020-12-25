using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Tacmind.Networking
{
	public class InstantiateData : INetworkObject
	{
		public string ResourcePath { get; set; }

		public Vector3 Position { get; set; }
		public Quaternion Rotation { get; set; }
		public Vector3 Scale { get; set; }

		public ushort Owner { get; set; }

		public ushort Id { get; set; }

		public string Extra { get; set; } = "";

		public InstantiateData()
		{

		}

		public InstantiateData(string resourcePath, Vector3 position, Quaternion rotation, Vector3 scale, ushort owner, ushort id)
		{
			ResourcePath = resourcePath;
			Position = position;
			Rotation = rotation;
			Scale = scale;
			Owner = owner;
			Id = id;
		}


		public override string ToString()
		{
			return "Instantiate " + Owner + " : " + Id;
		}

		public void ReadFrom(NetworkStream ns)
		{
			ResourcePath = ns.ReadString();
			Position = ns.ReadVector3();
			Rotation = ns.ReadQuaternion();
			Scale = ns.ReadVector3();

			Owner = ns.ReadUShort();
			Id = ns.ReadUShort();

			Extra = ns.ReadString();
		}

		public void WriteTo(NetworkStream ns)
		{
			ns.WriteString(ResourcePath);
			ns.WriteVector3(Position);
			ns.WriteQuaternion(Rotation);
			ns.WriteVector3(Scale);
			ns.WriteUShort(Owner);
			ns.WriteUShort(Id);
			ns.WriteString(Extra);
		}
	}
}
