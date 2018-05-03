import tensorflow as tf
import os;

class Saver:

    _check_point_path = "./CheckPoint"

    def __init__(self, name : str, sess : tf.Session):
        self._sess = sess
        self._name = name;
        
        self._CreateDirection();
        self._CreateSaver();

    def _CreateDirection(self):
        self._dir = self._check_point_path + "/" + self._name + "/"

        if not os.path.exists(self._dir):
            os.makedirs(self._dir)

    def _CreateSaver(self):
         self._saver = tf.train.Saver()

    def CheckRestore(self):
        checkpoint = tf.train.get_checkpoint_state(self._dir)

        if checkpoint and checkpoint.model_checkpoint_path:
            try:
                self._saver.restore(self._sess, checkpoint.model_checkpoint_path)
                print("Successfully loaded:", checkpoint.model_checkpoint_path)
            except:
                print("Error on loading old network weights")
        else:
            print("Could not find old network weights")

    def Save(self):
        self._saver.save(self._sess, self._dir)