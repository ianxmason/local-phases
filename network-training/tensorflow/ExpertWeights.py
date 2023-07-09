"""
Class of ExpertWeights
"""

import numpy as np
import tensorflow as tf

class ExpertWeights(object):
    def __init__(self, rng, shape , name):
        """rng"""
        self.initialRNG   = rng
        
        """shape"""
        self.weight_shape =  shape                    #4/8 * out * in
        self.bias_shape   =  (shape[0],shape[1],1)    #4/8 * out * 1
        
        """alpha and beta"""
        self.alpha        =  tf.Variable(self.initial_alpha(), name=name+'alpha') 
        self.beta         =  tf.Variable(self.initial_beta(),  name=name+'beta') 

        self.name = name
    
        
    """initialize parameters for experts i.e. alpha and beta"""
    def initial_alpha_np(self):
        shape = self.weight_shape
        rng   = self.initialRNG
        alpha_bound = np.sqrt(6. / np.prod(shape[-2:]))
        alpha = np.asarray(
            rng.uniform(low=-alpha_bound, high=alpha_bound, size=shape),
            dtype=np.float32)
        return alpha
    
    def initial_alpha(self):
        alpha = self.initial_alpha_np()
        return tf.convert_to_tensor(alpha, dtype = tf.float32)
    
    def initial_beta(self):
        return tf.zeros(self.bias_shape, tf.float32)

    def get_NNweight(self, controlweights):
        r = tf.einsum('ij, jkl -> ikl', controlweights, self.alpha)  #?*out*in
        return r                          
        
    def get_NNbias(self, controlweights): 
        r = tf.einsum('ij, jkl -> ikl', controlweights, self.beta)  # ?*out*1
        return r

    def load_expert_from_arr(self, arr):
        alpha_const = arr[0]
        beta_const = arr[1]      
        """Override alpha and beta with constants so they become untrainable """
        self.alpha = tf.constant(alpha_const, dtype=tf.float32, name=self.name+'alpha')
        self.beta = tf.constant(beta_const, dtype=tf.float32, name=self.name+'beta')



def save_EP(alpha, beta, filename, num_experts):
    for i in range(len(alpha)):
        for j in range(num_experts):
            a = alpha[i][j]
            b = beta[i][j]
            a.tofile(filename+'/cp%0i_a%0i.bin' % (i,j))
            b.tofile(filename+'/cp%0i_b%0i.bin' % (i,j))


