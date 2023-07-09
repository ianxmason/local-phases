"""
Class of Gating NN
"""
import numpy as np
import tensorflow as tf

class Gating(object):
    def __init__(self, num_layers, units, gating_indices, dropout_rate, np_seed=23456, activation=tf.nn.elu, vscope="Gating"):
        """rng"""        
        self.num_layers = num_layers
        self.gating_indices = gating_indices

        if len(units)!=num_layers or not all([isinstance(i,list) for i in units]) or not all([len(i)==2 for i in units]):
            raise ValueError("units should be length num_layers and contain lists of length 2")
        else:
            self.units = units

        """dropout"""
        if type(dropout_rate) == int or type(dropout_rate) == float:
            self.dropout_rate = [float(dropout_rate)] * num_layers
        elif type(dropout_rate) == list and len(dropout_rate) == num_layers:
            self.dropout_rate = dropout_rate
        else:
            raise ValueError("dropout_rate should be integer, float or a list of length num_layers")
        
        self.rng = np.random.RandomState(np_seed)
        self.activation = activation
        self.vscope = vscope
  
        with tf.variable_scope(self.vscope) as self.vs:
            self.test_frame_ph = tf.placeholder(tf.float32, shape=[None, self.units[0][0]], name="test_gating_ph")
            self.train_flag_ph = tf.placeholder(tf.bool, name="train_flag_ph")

        # """"output blending coefficients"""
        # self.BC   = self.fp()
        
        
    """initialize parameters """
    def initial_weight(self, shape):
        rng   = self.rng
        weight_bound = np.sqrt(6. / np.sum(shape[-2:]))
        weight = np.asarray(
            rng.uniform(low=-weight_bound, high=weight_bound, size=shape),
            dtype=np.float32)
        return tf.convert_to_tensor(weight, dtype = tf.float32)
    
    def initial_bias(self, shape):
        return tf.zeros(shape, tf.float32)
    

    def __call__(self, next_frame, pretrained_weights=None):
        with tf.variable_scope(self.vs, auxiliary_name_scope=False) as self.vs1:
            with tf.name_scope(self.vs1.original_name_scope):
                self.pretrained_weights = pretrained_weights
                # in_feats = tf.cond(self.train_flag_ph, lambda : next_frame[:,:-1], lambda : self.test_frame_ph)

                in_feats = tf.cond(self.train_flag_ph, lambda : tf.gather(next_frame, self.gating_indices, axis=1), 
                                                       lambda : tf.gather(self.test_frame_ph, self.gating_indices, axis=1))

                if self.pretrained_weights is not None:  # are untrainable, for finetuning FiLM generator only. Because tf.constant is untrainable.
                    self.Weights = [tf.constant(self.pretrained_weights[2*l], dtype=tf.float32, name = 'wc{}_w'.format(l)) for l in range(self.num_layers)]
                    self.Biases = [tf.constant(self.pretrained_weights[2*l + 1], dtype=tf.float32, name = 'wc{}_b'.format(l)) for l in range(self.num_layers)]
                else:
                    self.Weights = [tf.Variable(self.initial_weight([self.units[l][1], self.units[l][0]]), name = 'wc{}_w'.format(l)) for l in range(self.num_layers)]
                    self.Biases = [tf.Variable(self.initial_bias([self.units[l][1], 1]), name = 'wc{}_b'.format(l)) for l in range(self.num_layers)]

                # H = tf.expand_dims(in_feats, -1)
                H = tf.transpose(in_feats)
                for l in range(self.num_layers):
                    H = tf.layers.dropout(inputs=H, rate=self.dropout_rate[l], training=self.train_flag_ph)
                    H = tf.matmul(self.Weights[l], H) + self.Biases[l]
                    if l != self.num_layers-1:
                        H = self.activation(H)
                    else:
                        H = tf.nn.softmax(H, dim=0)                      

                return tf.transpose(H, name="BlendingCoeffs")



#--------------------------------------get the input for the Gating network---------------------------------
"""global parameters"""
# num_trajPoints       = 12 #number of trajectory points
# num_trajUnit_noSpeed = 6  #number of trajectory units: Position X,Z; Direction X,Z; Velocity X,Z;           
# num_trajUnit_speed   = 7  #number of trajectory units: Position X,Z; Direction X,Z; Velocity X,Z; Speed
# num_jointUnit        = 12 #number of joint units: PositionXYZ Rotation VelocityXYZ


# #get the velocity of joints, desired velocity and style
# def getInput(data, index_joint):    
#     gating_input = data[..., index_joint[0]:index_joint[0]+1]
#     index_joint.remove(index_joint[0])
#     for i in index_joint:
#         gating_input  = tf.concat( [gating_input, data[...,i:i+1]],axis = -1)
#     return gating_input 


def save_GT(weight, bias, filename):
    for i in range(len(weight)):
        a = weight[i]
        b = bias[i]
        a.tofile(filename+'/wc%0i_w.bin' % i)
        b.tofile(filename+'/wc%0i_b.bin' % i)

