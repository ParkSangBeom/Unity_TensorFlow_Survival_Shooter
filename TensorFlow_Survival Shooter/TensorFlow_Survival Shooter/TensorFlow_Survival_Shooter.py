import tensorflow as tf
import numpy as np
import threading
import socket
import json
import os
from random import *
import Saver as sv

os.environ['CUDA_VISIBLE_DEVICES'] = '-1'

HOST = "127.0.0.1"
PORT = 9051

PG = None
sess = None

HEIGHT_SIZE = 31
WIDTH_SIZE = 31
DEPTH_SIZE = 1

INPUT_SIZE = HEIGHT_SIZE * WIDTH_SIZE * DEPTH_SIZE
OUTPUT_SIZE = 36 * 2

DISCOUNT = 0.0
EPSILON = 1e-8
STEP = 0

BUFF_SIZE = 10000000

def RunServer():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        global PG

        s.bind((HOST, PORT))
        print("서버 대기중...")

        s.listen(10) # 듣기.
        conn, addr = s.accept() # 수락
        print("===[클라이언트 접속]===")

        buffer_obs_list = []
        buffer_act_list = []
        buffer_rew_list = []

        while True:
            obs_list = []
            act_list = []
            rew_list = []

            msg = conn.recv(BUFF_SIZE) # 읽기.
            msg = msg.decode('utf-8')
            try:
                json_data = json.loads(msg)
            except:
                print("Error!!!")
                result = json.dumps({"result" : "train"})
                msg = bytes(result, 'utf-8')
                conn.sendall(msg) #보내기

            type = json_data["type"]
            if type == "action":
                data_list = json_data["datalist"]                
                for data in data_list:

                    stack =  np.zeros(INPUT_SIZE, dtype = np.float)
                    for i in range(INPUT_SIZE):
                        key = "ob_" + str(i)
                        ob = data[key]
                        stack[i] = ob;

                    stack = np.reshape(stack, [1, HEIGHT_SIZE, WIDTH_SIZE, DEPTH_SIZE])
                    obs_list.append(stack)

                obs_list = np.vstack(obs_list)                

                action = PG.GetAction(obs_list)
                result = json.dumps({"result" : "action", "value" : str(action)})
                msg = bytes(result, 'utf-8')
                conn.sendall(msg) #보내기

            if type == "train":
                data_list = json_data["datalist"]                
                end = json_data["end"]      
                for data in data_list:

                    stack =  np.zeros(INPUT_SIZE, dtype = np.float)
                    for i in range(INPUT_SIZE):
                        key = "ob_" + str(i)
                        ob = data[key]
                        stack[i] = ob;

                    stack = np.reshape(stack, [1, HEIGHT_SIZE, WIDTH_SIZE, DEPTH_SIZE])
                    buffer_obs_list.append(stack)

                    action = data["action"]
                    action = OneHot(int(action))
                    buffer_act_list.append(action)

                    reward = data["reward"]
                    buffer_rew_list.append(reward)

                if end == "1":

                    buffer_obs_list = np.vstack(buffer_obs_list)
                    buffer_act_list = np.vstack(buffer_act_list)
                    buffer_rew_list = DiscountRewards(buffer_rew_list)

                    PG.Train(buffer_obs_list, buffer_act_list, buffer_rew_list)
                    result = json.dumps({"result" : "train"})
                    msg = bytes(result, 'utf-8')
                    conn.sendall(msg) #보내기

                    buffer_obs_list = []
                    buffer_act_list = []
                    buffer_rew_list = []

        conn.close() # 닫기

class PolicyGradientNetwork:
    _LEARNING_RATE = 1e-3
    _HIDDEN_SIZE = 100

    def __init__(self, sess, height_size, width_size, depth_size, output_size, name = "NoneNetwork"):
        self.sess = sess
        self.height_size = height_size
        self.width_size = width_size
        self.depth_size = depth_size
        #self.input_size = input_size
        self.output_size = output_size
        self.name = name

        self._BuildNetwork();
        self.sess.run(tf.global_variables_initializer())

        self._saver = sv.Saver(name, sess)
        self._SetSaver()

        print("TF준비 완료!")

    def _BuildNetwork(self):
        with tf.variable_scope(self.name):
            #self.observation = tf.placeholder(dtype = tf.float32, shape = [None, self.input_size])
            self.observation = tf.placeholder(dtype = tf.float32, shape = [None, self.height_size, self.width_size, self.depth_size])
            self.action = tf.placeholder(dtype = tf.float32, shape = [None, self.output_size])
            self.reward = tf.placeholder(dtype = tf.float32, shape = [None, 1])

            with tf.name_scope("Conv1"):
                F1 = tf.Variable(tf.random_normal([3, 3, 1, 8], stddev=0.01))
                L1 = tf.nn.conv2d(self.observation, F1, strides=[1, 1, 1, 1], padding='VALID')
                L1 = tf.nn.relu(L1)
                L1 = tf.nn.max_pool(L1, ksize=[1, 2, 2, 1], strides=[1, 2, 2, 1], padding='VALID')

            with tf.name_scope("Conv2"):
                F2 = tf.Variable(tf.random_normal([3, 3, 8, 16], stddev=0.01))
                L2 = tf.nn.conv2d(L1, F2, strides=[1, 1, 1, 1], padding='VALID')
                L2 = tf.nn.relu(L2)
                self.L2_flat = tf.reshape(L2, [-1, L2.shape[1] * L2.shape[2] * L2.shape[3]])

            W1 = tf.get_variable("W1", shape = [self.L2_flat.shape[1], self._HIDDEN_SIZE], initializer = tf.contrib.layers.xavier_initializer())
            b1 = tf.get_variable("b1", shape = [self._HIDDEN_SIZE])
            L1 = tf.nn.relu(tf.matmul(self.L2_flat, W1) + b1)

            W2 = tf.get_variable("W2", shape = [self._HIDDEN_SIZE, self.output_size], initializer = tf.contrib.layers.xavier_initializer())
            b2 = tf.get_variable("b2", shape = [self.output_size])
            self.logits = tf.nn.softmax(tf.matmul(L1, W2) + b2)

            self.get_action = self.logits
            #self.get_action = tf.reshape(tf.argmax(self.logits, 1), [])

            self.log_p = -self.action * tf.log(tf.clip_by_value(self.logits, EPSILON, 1.))
            self.log_lik = self.log_p * self.reward
            self.loss = tf.reduce_mean(tf.reduce_sum(self.log_lik, axis=1))
            self.train = tf.train.AdamOptimizer(self._LEARNING_RATE).minimize(self.loss)



    def Train(self, obs, act, rew):
        _, loss = self.sess.run([self.train, self.loss], feed_dict={self.observation : obs, self.action : act, self.reward : rew})
        print(loss)

        #global STEP
        #STEP += 1
        #self._Save(STEP)

    def GetAction(self, obs):
        action, logits = self.sess.run([self.get_action, self.logits], feed_dict={self.observation : obs})
        action = np.random.choice(np.arange(self.output_size), p=action[0])
        return action

    def _SetSaver(self):
        self._saver.CheckRestore()

    def _Save(self, step):
        if step != 0 and step % 10 == 0:
            print("SAVE!!")
            self._saver.Save()

def OneHot(value):
    zero = np.zeros(OUTPUT_SIZE, dtype = np.int)
    zero[value] = 1
    return  zero

def DiscountRewards(reward_memory):
    v_memory = np.vstack(reward_memory)
    discounted = np.zeros_like(v_memory, dtype=np.float32)
    add_value = 0
    length = len(reward_memory)

    for i in reversed(range(length)):
        if v_memory[i] < 0:
            add_value = 0
        add_value = v_memory[i] + (DISCOUNT * add_value)
        discounted[i] = add_value

    discounted -= np.mean(discounted)
    discounted /= (np.std(discounted) + EPSILON)

    #discounted = np.vstack(reward_memory)
    #discounted = np.reshape(discounted, [-1])
    return discounted

def Main():
    global PG
    global sess

    t = threading.Thread(target=RunServer)
    t.start()

    sess = tf.Session()
    PG = PolicyGradientNetwork(sess, HEIGHT_SIZE, WIDTH_SIZE, DEPTH_SIZE, OUTPUT_SIZE, "AA")

if __name__ == '__main__':
    Main()