
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tacmind.Logging;
using UnityEngine;

namespace Tacmind.Networking
{

	public class ReliablePacketCollection
	{


		public class ReliablePacket
		{
			public uint id;
			public NetworkStream buffer;
			public long issuedAt;
			public long timestamp;
			public int tries = 1;

			private bool poolAvailability;

			public void Clear()
			{
				tries = 1;
			}

			public override bool Equals(object obj)
			{
				ReliablePacket other = (ReliablePacket)obj;

				if (other == null)
				{
					return false;
				}

				return other.id == id;
			}

			public override int GetHashCode()
			{
				return id.GetHashCode();
			}

			public bool GetPoolAvailability()
			{
				return poolAvailability;
			}

			public void SetPoolAvailability(bool poolAvailability)
			{
				this.poolAvailability = poolAvailability;
			}
		}


		private readonly object locker = new object();


		public List<ReliablePacket> packets;

		private List<NetworkStream> pendingPackets;

		private Dictionary<uint, byte> acknowledgedIds;

		private Dictionary<uint, long> issuedTimestamps;

		private int waitMilliseconds = 100;
		private int maxTries = 10;

		public ReliablePacketCollection()
		{
			packets = new List<ReliablePacket>();
			pendingPackets = new List<NetworkStream>();
			acknowledgedIds = new Dictionary<uint, byte>();

			issuedTimestamps = new Dictionary<uint, long>();

		}

		public int GetCount()
		{
			return packets.Count;
		}

		public ReliablePacket Get(uint id)
		{
			return packets.Where(packet => packet.id == id).FirstOrDefault();
		}

		public long GetIssuedTime(uint id)
		{
			if (issuedTimestamps.ContainsKey(id))
			{
				return issuedTimestamps[id];
			}

			return -1;
		}

		public void Add(NetworkStream ns)
		{
			Monitor.Enter(locker);

			ReliablePacket packet = new ReliablePacket(); // pool.Recycle();
			packet.Clear();

			long timestamp = NetworkUtility.GetUnixTimestamp();
			//issuedTimestamps.Add(ns.UId, timestamp);

			packet.tries = 0;
			packet.id = ns.UId;
			packet.buffer = ns;
			packet.timestamp = timestamp + waitMilliseconds;

			packets.Add(packet);

			Monitor.Exit(locker);
		}

		public void Remove(uint id)
		{
			Monitor.Enter(locker);

			if (acknowledgedIds.ContainsKey(id) == false)
			{
				acknowledgedIds.Add(id, 0);
			}

			Monitor.Exit(locker);
		}

		public void ClearAcknowledgedPackets()
		{
			Monitor.Enter(locker);

			if (acknowledgedIds == null)
			{

				Log.WriteErrorLine("AcknowledgedIds is null!");
				return;
			}

			for (int i = packets.Count - 1; i >= 0; i--)
			{
				ReliablePacket packet = packets[i];
				if (packet == null || packet.tries >= maxTries)
				{
					packets.RemoveAt(i);
					continue;
				}
				if (acknowledgedIds.ContainsKey(packet.id))
				{
					//Log.WriteLine("Ack received after " + packet.tries + " tries.");
					packets.RemoveAt(i);

					/*if (issuedTimestamps.ContainsKey(packet.id))
					{
						issuedTimestamps.Remove(packet.id);
					}*/

					//Log.WriteLine("Acknowledged");
				}
			}

			acknowledgedIds.Clear();

			Monitor.Exit(locker);

		}

		public List<NetworkStream> GetPendingPackets()
		{
			pendingPackets.Clear();

			long time = NetworkUtility.GetUnixTimestamp();

			ReliablePacket packet;

			//List<ReliablePacket> buffers = new List<ReliablePacket>(packets);

			for (int i = 0; i < packets.Count; i += 1)
			{
				packet = packets[i];
				if (packet.timestamp < time)
				{
					pendingPackets.Add(packet.buffer);
					packet.timestamp = time + waitMilliseconds;
					packet.tries++;
				}
			}

			//packets = buffers;

			return pendingPackets;
		}
	}


}
