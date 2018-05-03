using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CompleteProject
{
    public class Agent : MonoBehaviour
    {
        public enum State
        {
            None = 0,
            Start,
            Loading,
            Update,
        }

        public SocketManager _socketManager = null;
        public ScoreManager _scoreManager = null;
        public EnemyManager _enemyManager = null;

        public GameObject _player = null;
        private PlayerMovement _playerMovement = null;
        private PlayerShooting _playerShooting = null;
        private PlayerHealth _playerHealth = null;       

        private State _state = State.None;
        private JsonParser _jParser = new JsonParser();
        private float _timeScale = 1.0f;
        private int _globalCount = 1;
        private int _step = 0;

        private List<JsonParser.TensorData> _curLsTensorData = new List<JsonParser.TensorData>();
        private JsonParser.TensorData _curTensorData = new JsonParser.TensorData();

        public void Init()
        {
            _playerMovement = _player.GetComponentInChildren<PlayerMovement>();
            _playerShooting = _player.GetComponentInChildren<PlayerShooting>();
            _playerHealth = _player.GetComponentInChildren<PlayerHealth>();
        }

        public void AgentStart()
        {
            _state = State.Start;
        }

        private void AgentReset()
        {
            _playerHealth.Init();
            _playerMovement.Init();
            _scoreManager.Init();
            _enemyManager.Init();

            _curLsTensorData.Clear();
            
            _globalCount += 1;
            _step = 0;
        }

        private void Update()
        {
            if (_state == State.Loading)
            {
                Time.timeScale = 0.0f;
                return;
            }
            else if (_state == State.Start)
            {
                AgentReset();

                _curTensorData = new JsonParser.TensorData();
                _curLsTensorData.Add(_curTensorData);

                List<float> lsOb = GetObservations();
                _curTensorData.LsObserver = lsOb;

                Time.timeScale = _timeScale;
                _state = State.Loading;
                string jsonData = _jParser.GetActionJsonData(_curTensorData);
                _socketManager.SendData(jsonData);
            }
            else if (_state == State.Update)
            {
                Time.timeScale = _timeScale;
                bool isShot = Step(_curTensorData.Action);

                bool damage = _playerHealth.IsDamaged();
                bool takeDamage = _playerShooting.IsTakeDamage();
                _curTensorData.Reward = 0.1f;

                if(isShot)
                {
                    if (takeDamage)
                    {
                        _curTensorData.Reward = 10f;
                    }
                    else
                    {
                        _curTensorData.Reward = -1f;
                    }
                }
                else if (damage)
                {
                    _curTensorData.Reward = -10f;
                }

                if (IsDone())
                {
                    //_curTensorData.Reward = -10f;
                    print("GlobalCount : " + _globalCount + "  [Step : " + _step + "]" + "[Reward : " + ScoreManager.score + "]");

                    _state = State.Loading;
                    List<JsonParser.TensorData> temp = new List<JsonParser.TensorData>();
                    for (int i = 0; i < _curLsTensorData.Count; ++i)
                    {
                        temp.Add(_curLsTensorData[i]);
                        if (temp.Count == 300)
                        {
                            string trainJsonData = _jParser.GetTrainJsonData(temp, _curLsTensorData.Count - 1 == i);
                            _socketManager.SendData(trainJsonData);
                            temp.Clear();
                        }
                        else if(i == _curLsTensorData.Count - 1)
                        {
                            string trainJsonData = _jParser.GetTrainJsonData(temp, true);
                            _socketManager.SendData(trainJsonData);
                            temp.Clear();
                        }
                    }
                    return;
                }

                _curTensorData = new JsonParser.TensorData();
                _curLsTensorData.Add(_curTensorData);

                List<float> lsOb = GetObservations();
                _curTensorData.LsObserver = lsOb;

                _step++;
                _state = State.Loading;
                string jsonData = _jParser.GetActionJsonData(_curTensorData);
                _socketManager.SendData(jsonData);
            }
        }

        private List<float> GetObservations()
        {
            List<float> lsOb = new List<float>();

            int filterSize = 15;
            float extend = 0.4f;
            Vector3 halfExtend = new Vector3(extend, extend, extend);
            float x = _player.transform.position.x;
            float z = _player.transform.position.z;

            for (int i = -filterSize; i < filterSize + 1; i++)
            {
                for (int k = -filterSize; k < filterSize + 1; k++)
                {
                    float cencondition = 0.0f;
                    if (i == 0 && k == 0)
                    {
                        cencondition = _player.transform.rotation.y;
                        lsOb.Add(cencondition);
                        continue;
                    }

                    Vector3 center = new Vector3(x + (k * extend), 0.0f, z + (i * extend));
                    Collider[] colls = Physics.OverlapBox(center, halfExtend);
                    foreach (Collider coll in colls)
                    {
                        string tag = coll.transform.gameObject.tag;
                        if (tag == "Monster")
                        {
                            cencondition = 2.0f;
                            break;
                        }
                        if (tag == "Wall")
                        {
                            cencondition = 3.0f;
                        }
                    }
                    lsOb.Add(cencondition);
                    Debug.DrawRay(center, halfExtend, Color.red);
                }
            }

            //lsOb.Add(_player.transform.rotation.y);
            return lsOb;
        }

        private bool Step(int action)
        {
            bool IsShot = false;
            int rot = action % 36;
            int move = UnityEngine.Random.Range(0, 5);
            int shot = action % 2;

            switch (move)
            {
                case 0:
                    _playerMovement.MoveAgent(1, 0);
                    break;

                case 1:
                    _playerMovement.MoveAgent(-1, 0);
                    break;

                case 2:
                    _playerMovement.MoveAgent(0, 1);
                    break;

                case 3:
                    _playerMovement.MoveAgent(0, -1);
                    break;
            }

            switch (shot)
            {
                case 0:
                    _player.transform.rotation = Quaternion.Euler(new Vector3(0.0f, rot * 10, 0.0f));
                    _playerShooting.AgentShoot();
                    IsShot = true;
                    break;

                case 1:
                    IsShot = false;
                    break;
            }

            //_player.transform.rotation = Quaternion.Euler(new Vector3(0.0f, rot * 10, 0.0f));
            //_player.transform.Rotate(new Vector3(0.0f, 1.0f, 0.0f), action * 10);
            //_playerShooting.AgentShoot();
            //_playerMovement.MoveAgent(x, z);
            return IsShot;
        }

        private bool IsDone()
        {
            bool isDone = _playerHealth.IsDead();
            return isDone;
        }

        public void ListenForData(string data)
        {
            string result = _jParser.GetResultData(data);
            if(result == "action")
            {
                int action = _jParser.GetActionData(data);
                _curTensorData.Action = action;
                _state = State.Update;
            }
            else if(result == "train")
            {
                AgentStart();
            }
        }
    }
}