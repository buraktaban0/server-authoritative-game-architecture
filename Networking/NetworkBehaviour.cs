using Tacmind.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace Tacmind.Networking
{
	public abstract class NetworkBehaviour : MonoBehaviour, INetworkEntity
	{
		public bool ownerIsServer = false;

		public bool offlineMode = false;

		public bool IsMine { get { return OwnerId == NetworkManager.LocalId; } }

		public NetworkTarget Owner { get; set; }

		[SerializeField] private ushort ownerId = 2;
		public ushort OwnerId { get => ownerId; set { ownerId = value; } }

		//[HideInInspector]
		[SerializeField]
		private ushort entityId;
		public ushort EntityId { get { return entityId; } set { entityId = value; } }


		public virtual void Awake()
		{
			if (offlineMode)
			{
				InitializeOffline();
			}
		}

		public virtual void InitializeOffline()
		{

		}

		public ushort GetEntityId()
		{
			return EntityId;
		}

		public void SetEntityId(ushort id)
		{
			EntityId = id;
		}


		public abstract void OnReceive(NetworkStream ns);

		public abstract void OnSend(NetworkStream ns, ushort targetId);

		public abstract void OnSendEnd();

		public ushort GetOwnerId()
		{
			return OwnerId;
		}

		public static List<NetworkBehaviour> GetAllServerOwned()
		{
			return GameObject.FindObjectsOfType<NetworkBehaviour>().Where(beh => beh.ownerIsServer).ToList();
		}
	}
}

