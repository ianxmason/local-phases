import numpy as np
import tensorflow as tf
import Gating as GT
from Gating import Gating
import ExpertWeights as EW
from ExpertWeights import ExpertWeights
from AdamWParameter import AdamWParameter
from AdamW import AdamOptimizer
import Utils as utils


class LPN(object):
    def __init__(self, num_layers, num_experts, units, dropout_rate, np_seed=23456, activation=tf.nn.elu, vscope="LPN"):
        
        self.num_layers = num_layers
        self.num_experts = num_experts

        if len(units)!=num_layers or not all([isinstance(i,list) for i in units]) or not all([len(i)==2 for i in units]):
            raise ValueError("units should be length num_layers and contain lists of length 2")
        else:
            self.units = units

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
            # self.test_phase_ph = tf.placeholder(tf.float32, shape=[None], name="test_phase_ph")
            self.test_frame_ph = tf.placeholder(tf.float32, shape=[None, self.units[0][0]], name="test_frame_ph")
            self.train_flag_ph = tf.placeholder(tf.bool, name="train_flag_ph")
                
        
    def __call__(self, next_frame, BC, FiLM_Params=None, pretrained_weights=None):
        with tf.variable_scope(self.vs, auxiliary_name_scope=False) as self.vs1:
            with tf.name_scope(self.vs1.original_name_scope):
                self.BC = BC
                self.FiLM_Params = FiLM_Params
                self.pretrained_weights = pretrained_weights

                in_feats = tf.cond(self.train_flag_ph, lambda : next_frame[:,:-8], lambda : self.test_frame_ph)

                self.Layers = [ExpertWeights(self.rng, (self.num_experts, self.units[l][1],  self.units[l][0]), 'layer{}'.format(l)) for l in range(self.num_layers)]
        
                if self.pretrained_weights is not None:  # are untrainable, not for finetuning
                    for i in range(len(self.Layers)):
                        self.Layers[i].load_expert_from_arr(self.pretrained_weights[i])

                self.Weights = [layer.get_NNweight(self.BC) for layer in self.Layers]
                self.Biases = [layer.get_NNbias(self.BC) for layer in self.Layers]


                H = tf.expand_dims(in_feats, -1)
                for l in range(self.num_layers):
                    H = tf.layers.dropout(inputs=H, rate=self.dropout_rate[l], training=self.train_flag_ph)
                    H = tf.matmul(self.Weights[l], H) + self.Biases[l]
                    if l != self.num_layers-1:  # don't do the below for the last layer
                        if self.FiLM_Params is not None:
                            """
                            Layer Norm
                            """
                            m = tf.expand_dims(tf.reduce_mean(H, axis=1), -1)
                            s = tf.expand_dims(tf.sqrt(tf.reduce_sum(tf.square(H - m), axis=1)/(self.units[l][1]-1)), -1)
                            H = (H - m) / (s + 1e-6)
                            """
                            Apply FiLM parameters
                            """
                            start_idx = sum([self.units[i][1] for i in range(l)])*2
                            H = self.FiLM_Params[:,start_idx:start_idx+self.units[l][1]] * H + self.FiLM_Params[:,start_idx+self.units[l][1]:start_idx+self.units[l][1]*2]
                        H = self.activation(H) 


                return tf.squeeze(H, -1, name = "Output")
   

        

        
                
        





    # def train(self):
    #     self.sess.run(tf.global_variables_initializer())
    #     saver = tf.train.Saver()
        
    #     """training"""
    #     print("total_batch:", self.total_batch)
    #     #randomly select training set
    #     I = np.arange(self.size_data)
    #     self.rng.shuffle(I)
    #     error_train = np.ones(self.epoch)
    #     #saving path
    #     model_path   = self.savepath+ '/model'
    #     nn_path      = self.savepath+ '/nn'
    #     weights_path = self.savepath+ '/weights'
    #     utils.build_path([model_path, nn_path, weights_path])
        
    #     #start to train
    #     print('Learning starts..')
    #     for epoch in range(self.epoch):
    #         avg_cost_train = 0
    #         for i in range(self.total_batch):
    #             index_train = I[i*self.batch_size:(i+1)*self.batch_size]
    #             batch_xs = self.input_data[index_train]
    #             batch_ys = self.output_data[index_train]
    #             clr, wdc = self.AP.getParameter(epoch)   #currentLearningRate & weightDecayCurrent
    #             feed_dict = {self.nn_X: batch_xs, self.nn_Y: batch_ys, self.nn_keep_prob: self.keep_prob_ini, self.nn_lr_c: clr, self.nn_wd_c: wdc}
    #             l, _, = self.sess.run([self.loss, self.optimizer], feed_dict=feed_dict)
    #             avg_cost_train += l / self.total_batch
                
    #             if i % 1000 == 0:
    #                 print(i, "trainingloss:", l)
    #                 print('Epoch:', '%04d' % (epoch + 1), 'clr:', clr)
    #                 print('Epoch:', '%04d' % (epoch + 1), 'wdc:', wdc)
                    
    #         #print and save training test error 
    #         print('Epoch:', '%04d' % (epoch + 1), 'trainingloss =', '{:.9f}'.format(avg_cost_train))
            
    #         error_train[epoch] = avg_cost_train
    #         error_train.tofile(model_path+"/error_train.bin")

    #         #save model and weights
    #         saver.save(self.sess, model_path+"/model.ckpt")
    #         GT.save_GT((self.sess.run(self.gatingNN.w0), self.sess.run(self.gatingNN.w1), self.sess.run(self.gatingNN.w2)), 
    #                    (self.sess.run(self.gatingNN.b0), self.sess.run(self.gatingNN.b1), self.sess.run(self.gatingNN.b2)), 
    #                    nn_path
    #                    )
    #         EW.save_EP((self.sess.run(self.layer0.alpha), self.sess.run(self.layer1.alpha), self.sess.run(self.layer2.alpha)),
    #                    (self.sess.run(self.layer0.beta), self.sess.run(self.layer1.beta), self.sess.run(self.layer2.beta)),
    #                    nn_path,
    #                    self.num_experts
    #                    )
            
    #         if epoch%10==0:
    #             weights_nn_path = weights_path + '/nn%03i' % epoch
    #             utils.build_path([weights_nn_path])
    #             GT.save_GT((self.sess.run(self.gatingNN.w0), self.sess.run(self.gatingNN.w1), self.sess.run(self.gatingNN.w2)), 
    #                        (self.sess.run(self.gatingNN.b0), self.sess.run(self.gatingNN.b1), self.sess.run(self.gatingNN.b2)), 
    #                        weights_nn_path
    #                        )
    #             EW.save_EP((self.sess.run(self.layer0.alpha), self.sess.run(self.layer1.alpha), self.sess.run(self.layer2.alpha)),
    #                        (self.sess.run(self.layer0.beta), self.sess.run(self.layer1.beta), self.sess.run(self.layer2.beta)),
    #                        weights_nn_path,
    #                        self.num_experts
    #                        )
    #     print('Learning Finished')
