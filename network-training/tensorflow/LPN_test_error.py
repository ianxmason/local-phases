import sys
import numpy as np
import yaml
import time
import unicodedata
import tensorflow as tf
from tensorflow.python.saved_model import tag_constants

# For Reproducible
# tf.set_random_seed(1234)
# Global Variables
byte_per_float32 = 4

with open('LPN_100Style25B_BLL.yml') as f:    
    config = yaml.load(f, Loader=yaml.FullLoader)

# with open('LPN_FiLM_Finetuning.yml') as f:    
#     config = yaml.load(f, Loader=yaml.FullLoader)

Xdim = config["Xdim"]
Ydim = config["Ydim"]
stats_path = config["stats_path"]
model_path = config["ft_model_path"]
styles_list = config["styles_list"]
styles_list.sort()

name_ep_input_tensor = "LPN/test_frame_ph:0"
name_gt_input_tensor = "Gating/test_gating_ph:0"
name_batch_size_tensor = "batch_size_ph:0"
name_mode_tensor = "mode_ph:0"
# name_test_style_tensor = "test_style_ph:0"
name_clip_seed = "clip_seed_ph:0"
name_frame_seed = "frame_seed_ph:0"
name_clip_handle = "Clips/clip_handle_ph:0"
name_frame_handle = "Frames/frame_handle_ph:0"
name_train_flag_0 = "FiLM_Generator/train_flag_ph:0"
name_train_flag_1 = "LPN/train_flag_ph:0"
name_train_flag_2 = "Gating/train_flag_ph:0"
name_film_flag = "FiLM_Generator/film_flag_ph:0"
name_film_params = "FiLM_Generator/film_params_ph:0"
# name_output_tensor = "PFNN/Output:0" 
# name_output_tensor = "Square:0" 
name_output_tensor = "Mean:0"
name_bll_output_tensor = "truediv:0"  # want to include division by num bones
  

with tf.Session() as sess:

    tf.saved_model.loader.load(sess, [tag_constants.SERVING], model_path)
    graph = tf.get_default_graph()
    print("Tensorflow graph loaded")

    nn_ep_input_frame = graph.get_tensor_by_name(name_ep_input_tensor)
    nn_gt_input_frame = graph.get_tensor_by_name(name_gt_input_tensor)
    nn_batch_size = graph.get_tensor_by_name(name_batch_size_tensor)
    nn_mode = graph.get_tensor_by_name(name_mode_tensor)

    clip_init_iterators = [graph.get_operation_by_name("Clips/" + style + "/MakeIterator") for style in styles_list]
    frame_init_iterators = [graph.get_operation_by_name("Frames/" + style + "/MakeIterator") for style in styles_list]
    clip_handle_phs = [graph.get_tensor_by_name("Clips/" + style + "/IteratorToStringHandle:0") for style in styles_list]
    frame_handle_phs = [graph.get_tensor_by_name("Frames/" + style + "/IteratorToStringHandle:0") for style in styles_list]


    nn_clip_seed = graph.get_tensor_by_name(name_clip_seed)
    nn_frame_seed = graph.get_tensor_by_name(name_frame_seed)
    nn_clip_handle = graph.get_tensor_by_name(name_clip_handle)
    nn_frame_handle = graph.get_tensor_by_name(name_frame_handle)
    nn_train_flag_0 = graph.get_tensor_by_name(name_train_flag_0)
    nn_train_flag_1 = graph.get_tensor_by_name(name_train_flag_1)
    nn_train_flag_2 = graph.get_tensor_by_name(name_train_flag_2)
    nn_film_flag = graph.get_tensor_by_name(name_film_flag)
    nn_film_params = graph.get_tensor_by_name(name_film_params)

    # test_clips = graph.get_tensor_by_name("Clips/IteratorGetNext:0")
    test_frames = graph.get_tensor_by_name("Frames/IteratorGetNext:0")

    # Initializing datasets 
    clip_handles = [sess.run(handle_ph) for handle_ph in clip_handle_phs]
    frame_handles = [sess.run(handle_ph) for handle_ph in frame_handle_phs]

    for i, style in enumerate(styles_list):
        # reinitialize to start of dataset
        sess.run(clip_init_iterators[i], feed_dict={nn_batch_size: 1000, nn_mode: "Test", nn_clip_seed: 12345})
        sess.run(frame_init_iterators[i], feed_dict={nn_batch_size: 1000, nn_mode: "Test", nn_frame_seed: 12345})  

        # Creating output tensor
        output_tensor = graph.get_tensor_by_name(name_output_tensor)
        bll_output_tensor = graph.get_tensor_by_name(name_bll_output_tensor)

        tensor_out = sess.run([output_tensor, bll_output_tensor], feed_dict={nn_frame_handle: frame_handles[i], nn_clip_handle: clip_handles[i], nn_train_flag_0: False,
                                                        nn_train_flag_1: True, nn_train_flag_2: True, nn_film_flag: True, nn_film_params: np.ones((1,512*4)),
                                                        nn_ep_input_frame: np.zeros([1000, 348]), nn_gt_input_frame: np.zeros([1000, 8])})

        # tally the square errors
        if i == 0:
            sq_err = tensor_out[0]
            bll_err = tensor_out[1]
        else:
            sq_err = np.vstack((sq_err,tensor_out[0]))
            bll_err = np.vstack((bll_err,tensor_out[1]))


        print("{}/{}".format(i, len(styles_list)), end="\r")
        print(style)
        print(np.mean(sq_err))  # so far
        print(np.mean(bll_err))  # so far
        print("==========")
    print("Test error:")
    print(sq_err.shape)
    print(np.mean(sq_err))
    print("BLL error:")
    print(bll_err.shape)
    print(np.mean(bll_err))
