using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections;
using System.Collections.Generic;

public class NetworkManager : MonoBehaviour, INetEventListener
{
    public LoginPanelController LoginPanel;
    public JoyStickerController JoyStick;
    public Player playerGameObj;

    private string host = "localhost";
    private int port = 9050;
    private string connectionKey = "testejean";
    private NetClient _netClient;
    private GameObject player = null;
    private string playerId = "";
    private NetPeer serverPeer = null;

    private readonly int messageMaxLength = 200;
    private readonly char messageTypeSeparator = '#';
    private readonly char messageValuesSeparator = '!';

    public void Start()
    {
        _netClient = new NetClient(this, connectionKey);
        _netClient.DisconnectTimeout = 5 * 60 * 1000;
        _netClient.UnconnectedMessagesEnabled = true;
        _netClient.Start();
        //_netClient.Connect(host, port);

        Debug.Log("Game is start");
        JoyStick.gameObject.SetActive(false);
        LoginPanel.plaBtn.onClick.AddListener(OnClickPlayBtn);
        JoyStick.OnCommandMove += OnCommandMove;

        NetDataWriter writer = new NetDataWriter();
        writer.Put("CLIENT DISCOVERY REQUEST");
        _netClient.SendDiscoveryRequest(writer, port);
    }

    void Update()
    {
        _netClient.PollEvents();
        //if (!_netClient.IsConnected)
        //{
        //    NetDataWriter writer = new NetDataWriter();
        //    writer.Put("CLIENT DISCOVERY REQUEST");
        //    _netClient.SendDiscoveryRequest(writer, port);
        //}
    }

    void OnDestroy()
    {
        if (_netClient != null && playerId.Length > 0)
        {
            SendMessage(ToServerMessageType.USER_DISCONNECT, playerId);
            _netClient.Stop();
        }
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Debug.LogFormat("[Client {0}] connected to: {1}:{2}", _netClient.LocalEndPoint.Port, peer.EndPoint.Host, peer.EndPoint.Port);
        if (serverPeer == null)
        {
            serverPeer = peer;
            Debug.Log("got the server peer");

            StartCoroutine("CalltoServer");
        }
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason, int socketErrorCode)
    {
        Debug.Log("[Client] disconnected: " + disconnectReason);
    }

    public void OnNetworkError(NetEndPoint endPoint, int error)
    {
        Debug.LogWarning("[Client] error! " + error);
    }

    public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
    {
        Debug.LogFormat("[Client] ReceiveUnconnected {0}. From: {1}. Data: {2}", messageType, remoteEndPoint, reader.GetString(100));
        if (messageType == UnconnectedMessageType.DiscoveryResponse)
        {
            _netClient.Connect(remoteEndPoint);
        }
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {

    }

    public void OnNetworkReceive(NetPeer fromPeer, NetDataReader dataReader)
    {
        string completeMessage = dataReader.GetString(messageMaxLength);
        string[] arrMessageParts = completeMessage.Split(messageTypeSeparator);
        string messageType = arrMessageParts[0];
        string[] arrValues = arrMessageParts[1].Split(messageValuesSeparator);

        if (messageType == FromServerMessageType.USER_CONNECTED.ToString("D"))
        {
            OnUserConnected(arrValues);
        }
        else if (messageType == FromServerMessageType.PLAY.ToString("D"))
        {
            OnUserPlay(arrValues);
        }
        else if (messageType == FromServerMessageType.MOVE.ToString("D"))
        {
            OnUserMove(arrValues);
        }
        else if (messageType == FromServerMessageType.USER_DISCONNECTED.ToString("D"))
        {
            OnUserDisconnected(arrValues);
        }
        else
        {
            Debug.LogWarningFormat("[WARNING] Received a message with unknown type: {0}", completeMessage);
        }
    }

    //sent messages

    private IEnumerator CalltoServer()
    {
        yield return new WaitForSeconds(1f);

        Debug.Log("Send message to the server");
        SendMessage(ToServerMessageType.USER_CONNECT, "");
    }

    void OnCommandMove(Vector3 vec3)
    {
        if (player != null)
        {
            Player playerCom = player.GetComponent<Player>();

            string message =
                            playerCom.id + messageValuesSeparator +
                            playerCom.playerName + messageValuesSeparator +
                            vec3.x.ToString() + messageValuesSeparator +
                            vec3.y.ToString() + messageValuesSeparator +
                            vec3.z.ToString();

            SendMessage(ToServerMessageType.MOVE, message);
        }
        else
        {
            Debug.LogWarningFormat("[WARNING] Tried to move without a player!");
        }
    }

    void OnClickPlayBtn()
    {
        if (serverPeer == null)
        {
            LoginPanel.inputField.text = "Server peer unknown. Wait a minute and try again.";
        }
        else
        {
            if (LoginPanel.inputField.text != "")
            {
                string message = LoginPanel.inputField.text + messageValuesSeparator + 0f.ToString() + messageValuesSeparator + 0f.ToString() + messageValuesSeparator + 0f.ToString();
                SendMessage(ToServerMessageType.PLAY, message);
            }
            else
            {
                LoginPanel.inputField.text = "Please enter your name again ";
            }
        }
    }

    //received messages

    void OnUserConnected(string[] values)
    {
        //values:
        //0: id        //1: name        //2: x        //3: y        //4: z
        Debug.Log("got user: " + values[1]);

        GameObject player = GameObject.Find(values[1]) as GameObject;
        if (player == null)
        {
            GameObject otherPlater = GameObject.Instantiate(playerGameObj.gameObject, playerGameObj.position, Quaternion.identity) as GameObject;
            Player otherPlayerCom = otherPlater.GetComponent<Player>();
            otherPlayerCom.id = values[0];
            otherPlayerCom.playerName = values[1];
            otherPlater.transform.position = new Vector3(float.Parse(values[2]), float.Parse(values[3]), float.Parse(values[4]));
        } else
        {
            Debug.Log("user already exists: " + values[1]);
        }
    }

    void OnUserPlay(string[] values)
    {
        //values:
        //0: id        //1: name        //2: x        //3: y        //4: z

        LoginPanel.gameObject.SetActive(false);
        JoyStick.gameObject.SetActive(true);
        JoyStick.ActivejooyStick();

        player = GameObject.Instantiate(playerGameObj.gameObject, playerGameObj.position, Quaternion.identity) as GameObject;
        Player playerCom = player.GetComponent<Player>();

        playerCom.id = values[0];
        playerCom.playerName = values[1];
        playerId = playerCom.id;
        player.transform.position = new Vector3(float.Parse(values[2]), float.Parse(values[3]), float.Parse(values[4]));

        JoyStick.playerObj = player;
    }

    void OnUserMove(string[] values)
    {
        //values:
        //0: id,    //1: name       //2: x        //3: y        //4: z

        GameObject player = GameObject.Find(values[1]) as GameObject;

        if (player != null)
        {
            player.transform.position = new Vector3(float.Parse(values[2]), float.Parse(values[3]), float.Parse(values[4]));
        } else
        {
            string message = values[0];
            SendMessage(ToServerMessageType.GET_USER, message);
        }
    }

    void OnUserDisconnected(string[] values)
    {
        //values:
        //0: name

        Destroy(GameObject.Find(values[0]));
    }

    public void SendMessage(ToServerMessageType type, string message)
    {
        if (serverPeer != null)
        {
            string completeMessage = type.ToString("D") + messageTypeSeparator + message;
            NetDataWriter writer = new NetDataWriter();
            writer.Put(completeMessage);
            serverPeer.Send(writer, SendOptions.ReliableOrdered);
        }
        else
        {
            Debug.LogWarning("server peer unknown. The message will not be sent: " + type.ToString("D") + message);
        }
    }

    public enum FromServerMessageType
    {
        USER_CONNECTED = 0,
        PLAY = 1,
        MOVE = 2,
        USER_DISCONNECTED = 3
    }

    public enum ToServerMessageType
    {
        USER_CONNECT = 0,
        PLAY = 1,
        MOVE = 2,
        USER_DISCONNECT = 3,
        GET_USER = 4
    }
}