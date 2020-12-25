using Tacmind.Collections;
using Tacmind.Inputs;
using Tacmind.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Tacmind.Core;
using Tacmind.ResourceManagement;

namespace Tacmind.Networking
{
	public class NetworkTarget
	{
		public static List<NetworkTarget> All { get; private set; } = new List<NetworkTarget>();

		public string Name { get; set; } = "UnknownPlayer";

		public Game.Team Team { get; set; } = Game.Team.Blue;

		public Transform SpawnTransform { get; set; }

		public string Character { get; set; } = "Ricket";

		public string Key { get; set; } = "undefined";

		public ushort Id { get; set; } = 1;

		public ushort LastRttPacket { get; internal set; }

		public long LastPingTime { get; internal set; }

		public long LastReceivedAt { get; internal set; }
		public long SilentTime { get { return NetworkUtility.GetUnixTimestamp() - LastReceivedAt; } }

		public bool IsRemoved { get; set; } = false;

		public int botKills = 0, playerKills = 0, deaths = 0;
		public uint money = 0;

		public IPEndPoint RemoteEP { get; set; }

		public Queue<UserCommand> Commands { get; private set; } = new Queue<UserCommand>(64);

		public ConcurrentQueue<NetworkStream> CommandQueue { get; private set; } = new ConcurrentQueue<NetworkStream>();
		public ConcurrentQueue<NetworkStream> SnapshotQueue { get; private set; } = new ConcurrentQueue<NetworkStream>();
		public ConcurrentQueue<NetworkStream> EventQueue { get; private set; } = new ConcurrentQueue<NetworkStream>();

		internal ThreadSafeQueue<UserCommand> CommandsQueue { get; private set; } = new ThreadSafeQueue<UserCommand>();

		internal Dictionary<ushort, Dictionary<byte, NetworkStream>> dividedPackets = new Dictionary<ushort, Dictionary<byte, NetworkStream>>();

		public BlockingCollection<NetworkStream> SharedSendQueue { get; set; }

		public BoundedList<uint> receiveHistory = new BoundedList<uint>(1024 * 4);
		//public BoundedList<uint> reliableReceiveHistory = new BoundedList<uint>(256);

		public float RTTActual { get; internal set; } = 0f;
		public float RTTSmooth { get; internal set; } = 0f;
		public float TripTime { get { return RTTSmooth * 0.5f; } }

		private NetworkClient client;


		private ushort currentPacketId = 0, currentReliablePacketId = 0;

		private bool listenToUserCommands = true;


		private Pool<UserCommand> cmdPool;

		public NetworkTarget(IPEndPoint remoteEP, string key, ushort id)
		{
			cmdPool = new Pool<UserCommand>(1024 * 2);

			this.Key = key;

			LastReceivedAt = NetworkUtility.GetUnixTimestamp() + 1000 * 1;

			if (!All.Contains(this))
			{
				All.Add(this);
			}

			this.Id = id;

			RemoteEP = remoteEP;

		}

		public void SetClient(NetworkClient client)
		{
			this.client = client;
		}

		public void SyncStats()
		{

			if (PlayerInfoCollection.Instance == null)
			{
				return;
			}

			var p = PlayerInfoCollection.Instance.GetPlayerInfo(Id);

			if (p == null)
				return;

			p.PlayerKills = (ushort)playerKills;
			p.BotKills = (ushort)botKills;
			p.Deaths = (ushort)deaths;
			p.Money = money;

			return;

			var ns = NetworkManager.BeginEvent(NetworkEventType.StatsUpdate);
			ns.reliability = NetworkStream.Reliability.Reliable;
			ns.WriteByte((byte)botKills);
			ns.WriteByte((byte)playerKills);
			ns.WriteByte((byte)deaths);

			this.Send(ns);
		}

		public void Send(NetworkStream ns)
		{
			this.SendNow(ns);

			return;

			ns.Owner = this.Id;

			ns.RemoteEP = RemoteEP;
			ns.PacketId = currentPacketId++;
			//ns.IsReliable = false;
			ns.EndWrite();
			//SharedSendQueue.Add(ns);

			//SharedSendQueue.Add(ns);

			client.SendNow(ns);
		}

		/*public void SendReliable(NetworkStream ns)
		{
			ns.RemoteEP = RemoteEP;
			ns.PacketId = currentReliablePacketId++;
			ns.IsReliable = true;
			SharedSendQueue.Add(ns);
		}*/

		public void SendNow(NetworkStream ns)
		{
			ns.Owner = this.Id;
			ns.RemoteEP = RemoteEP;
			ns.PacketId = currentPacketId++;
			ns.TotalParts = 0;
			ns.Part = 0;

			ns.EndWrite();

			client.SendNow(ns);
		}

		internal virtual void EnqueuePacket(NetworkStream ns)
		{
			//Log.WriteLine(ns.dataType);

			LastReceivedAt = NetworkUtility.GetUnixTimestamp();

			//uint id = ((uint)ns.PacketId << 16) | ((uint)ns.Part << 8) | (byte)ns.reliability;
			uint id = ns.PacketId;

			if (receiveHistory.Contains(id))
			{
				return;
			}

			receiveHistory.Add(id);

			if (ns.TotalParts > 1)
			{
				Dictionary<byte, NetworkStream> parts;
				if (!dividedPackets.TryGetValue(ns.PacketId, out parts))
				{
					parts = new Dictionary<byte, NetworkStream>();
					dividedPackets[ns.PacketId] = parts;
				}

				if (parts.ContainsKey(ns.Part))
				{
					return;
				}

				parts.Add(ns.Part, ns);

				if (parts.Count >= ns.TotalParts)
				{
					dividedPackets.Remove(ns.PacketId);

					ns = NetworkStream.JoinStreams(parts);

					/*byte[] b = new byte[1558];
					ns.ReadBytes(b);
					string s = ns.ReadString();
					Log.WriteErrorLine("|" + s + "|");*/
				}
				else
				{
					return;
				}

			}

			//Log.WriteErrorLine(ns.dataType);

			ns.Owner = this.Id;

			if (ns.dataType == NetworkStream.DataType.UserCommand)
			{
				if (listenToUserCommands)
				{
					UnpackCommands(ns);
				}
				return;
			}

			if (ns.dataType == NetworkStream.DataType.Snapshot)
			{
				SnapshotQueue.Enqueue(ns);
				return;
			}

			if (ns.dataType == NetworkStream.DataType.Event)
			{
				//Log.WriteErrorLine("EVENT RECV");
				EventQueue.Enqueue(ns);
				return;
			}

		}

		private void UnpackCommands(NetworkStream ns)
		{
			byte count = ns.ReadByte();

			for (int i = 0; i < count; i += 1)
			{
				UserCommand cmd = new UserCommand(); // cmdPool.Get();
				cmd.ReadFrom(ns);
				CommandsQueue.Enqueue(cmd);
			}


		}

		internal void GetCommandsReady()
		{
			if (CommandsQueue.BeginRead())
			{
				UserCommand cmd;
				while ((cmd = CommandsQueue.Dequeue()) != null)
				{
					Commands.Enqueue(cmd);
				}
				CommandsQueue.EndRead();
			}

		}

		public NetworkStream ReceiveSnapshot()
		{
			return Dequeue(SnapshotQueue);
		}

		public NetworkStream ReceiveCommand()
		{
			return Dequeue(CommandQueue);
		}

		public NetworkStream ReceiveEvent()
		{
			return Dequeue(EventQueue);
		}


		public void StopListeningToUserCommands()
		{
			listenToUserCommands = false;
			Commands.Clear();
			CommandsQueue.Clear();
		}

		public void StartListeningToUserCommands()
		{
			listenToUserCommands = true;
		}

		public override string ToString()
		{
			return RemoteEP.ToString() + "   " + RTTSmooth.ToString("0.0") + "ms";
		}

		private NetworkStream Dequeue(ConcurrentQueue<NetworkStream> queue)
		{
			NetworkStream ns;
			if (queue.TryDequeue(out ns))
			{
				return ns;
			}

			return null;
		}


		internal void UpdateRTT(float rttNew)
		{
			//Log.WriteErrorLine("rtt  " + rttNew);

			RTTActual = rttNew;

			float diff = rttNew - RTTSmooth;
			float max = 10000f;
			diff = Mathf.Clamp(diff * 0.1f, -max, max);
			RTTSmooth += diff;
		}

	}
}
