import numpy as np
import tensorflow as tf
import PFNNParameter as PFNNP
import Utils as utils
import yaml
from PFNNParameter import PFNNParameter
from AdamWParameter import AdamWParameter
from AdamW import AdamOptimizer
import os.path
from collections import defaultdict
from Conv1D_Dense import Conv1D_Dense
from Trainer import Trainer
from Data_Loader import Data_Loader
from Bll import bll
from LPN import LPN
from Gating import Gating


"""
Trains the LPN with FiLM parameters for each style.
"""

# Use this normally, ie. for training the model
with open('LPN_100Style25B_BLL.yml') as f:    
    config = yaml.load(f, Loader=yaml.FullLoader)

# Use this only for resaving model paths to get unseen styles test error and FiLM codes
# with open('LPN_FiLM_Finetuning.yml') as f:    
#     config = yaml.load(f, Loader=yaml.FullLoader)

tf.logging.set_verbosity(tf.logging.ERROR) # suppress deprecation warnings 
tf.set_random_seed(config["tf_seed"])

Data_Loader = Data_Loader(config)
next_frame, frame_handle_ph, frame_init_iterators = Data_Loader.load_frames()
next_clip, clip_handle_ph, clip_init_iterators = Data_Loader.load_clips()

FiLM_Generator = Conv1D_Dense(num_conv_layers=2, filters=[[256,25],[256, 25]],
                            num_dense_layers=2, units=[[int(config["clip_size"]/4)*256, 2048], [2048, 512*4]], # /4 because of 2 max pooling steps
                            dropout_rate=0.3, max_pooling=[[2,2],[2,2]], vscope="FiLM_Generator")

input_size = config["Xdim"] + config["one_hot_dim"]
output_size = config["Ydim"]


Gating = Gating(num_layers=3, units=[[len(config["gating_indices"]),32],[32,32],[32,config["num_experts"]]], gating_indices=config["gating_indices"], dropout_rate=0.3)

LPN = LPN(num_layers=3, num_experts=config["num_experts"], units=[[input_size,512],[512,512],[512,output_size]], dropout_rate=0.3)

model = lambda x, y: LPN(x, Gating(x), FiLM_Generator(y))
mse = lambda x, y: tf.reduce_mean(tf.square(x-y)) + bll(x, y, config["num_bones"], config["parents"], Data_Loader.out_mean, Data_Loader.out_std, offset=24)
trainer = Trainer(model=model, inputs=[next_frame[0], next_clip], outputs=[next_frame[1]], 
                iterators=[clip_init_iterators, frame_init_iterators], reinitialize=[False, True], config=config)

print(next_frame[0].shape)
print(next_clip.shape)

if __name__ == "__main__":
    train_sess = tf.Session()

    trainer.train_styles(mse, config["learning_rate"], config["batch_size"], config["num_epochs"], config["styles_list"], config["save_path"], config["model_name"], train_sess,
                itrtr_feed_dict={Data_Loader.batch_size_ph: config["batch_size"], Data_Loader.mode_ph: "Train", Data_Loader.clip_seed_ph: 12345, Data_Loader.frame_seed_ph: 12345},
                feed_dict={clip_handle_ph: None, frame_handle_ph: None,
                            FiLM_Generator.on_off_ph: True,
                            FiLM_Generator.params_ph: np.ones((config["batch_size"],512*4)),
                            FiLM_Generator.train_flag_ph: True, LPN.train_flag_ph: True,                        
                            LPN.test_frame_ph: np.zeros((config["batch_size"], input_size)),
                            Gating.train_flag_ph: True, Gating.test_frame_ph: np.zeros((config["batch_size"], len(config["gating_indices"])))},
                handle_keys = [clip_handle_ph, frame_handle_ph],
                save_scopes = {"LPN_":LPN.vscope, "Gating_":Gating.vscope, "FiLM_":FiLM_Generator.vscope})

    # Use this for resaving model paths to get unseen style error and FiLM codes 
    # trainer.resave_model(mse, config["learning_rate"], config["batch_size"], config["num_epochs"], config["styles_list"], config["save_path"], config["model_name"], train_sess,
    #             itrtr_feed_dict={Data_Loader.batch_size_ph: config["batch_size"], Data_Loader.mode_ph: "Train", Data_Loader.clip_seed_ph: 12345, Data_Loader.frame_seed_ph: 12345},
    #             feed_dict={clip_handle_ph: None, frame_handle_ph: None,
    #                         FiLM_Generator.on_off_ph: True,
    #                         FiLM_Generator.params_ph: np.ones((config["batch_size"],512*4)),
    #                         FiLM_Generator.train_flag_ph: True, LPN.train_flag_ph: True,                        
    #                         LPN.test_frame_ph: np.zeros((config["batch_size"], input_size)),
    #                         Gating.train_flag_ph: True, Gating.test_frame_ph: np.zeros((config["batch_size"], len(config["gating_indices"])))},
    #             handle_keys = [clip_handle_ph, frame_handle_ph],
    #             save_scopes = {"FiLM_":FiLM_Generator.vscope})

    train_sess.close()
