import sys
import numpy as np
import yaml
import time
import unicodedata
import tensorflow as tf
from tensorflow.python.saved_model import tag_constants
from collections import defaultdict
import pickle

with open('LPN_100Style25B_BLL.yml') as f:    
    config = yaml.load(f, Loader=yaml.FullLoader)

# with open('LPN_FiLM_Finetuning.yml') as f:    
#     config = yaml.load(f, Loader=yaml.FullLoader)

num_codes = 50  # We can set batch size to e.g. 50 and get 50 codes per style (this is not all the codes but it is easy to implement this way)

Xdim = config["Xdim"]
Ydim = config["Ydim"]
stats_path = config["stats_path"]
model_path = config["ft_model_path"]

name_input_tensor = "LPN/test_frame_ph:0"
name_batch_size_tensor = "batch_size_ph:0"
name_mode_tensor = "mode_ph:0"
name_test_style_tensor = "test_style_ph:0"
name_clip_seed = "clip_seed_ph:0"
name_frame_seed = "frame_seed_ph:0"
name_clip_handle = "Clips/clip_handle_ph:0"
name_frame_handle = "Frames/frame_handle_ph:0"
name_train_flag_0 = "FiLM_Generator/train_flag_ph:0"
name_train_flag_1 = "LPN/train_flag_ph:0"
name_film_flag = "FiLM_Generator/film_flag_ph:0"
name_film_params = "FiLM_Generator/film_params_ph:0"
# name_output_tensor = "PFNN/Output:0" 
# name_output_tensor = "Square:0"
name_output_tensor = "FiLM_Generator/Final_Dense_Layer/BiasAdd:0"

train_styles_list = config["styles_list"]
train_styles_list.sort()

with tf.Session() as sess:

    tf.saved_model.loader.load(sess, [tag_constants.SERVING], model_path)
    graph = tf.get_default_graph()
    print("Tensorflow graph loaded")

    nn_input_frame = graph.get_tensor_by_name(name_input_tensor)
    nn_batch_size = graph.get_tensor_by_name(name_batch_size_tensor)
    nn_mode = graph.get_tensor_by_name(name_mode_tensor)

    clip_init_iterators = [graph.get_operation_by_name("Clips/" + style + "/MakeIterator") for style in train_styles_list]
    frame_init_iterators = [graph.get_operation_by_name("Frames/" + style + "/MakeIterator") for style in train_styles_list]
    clip_handle_phs = [graph.get_tensor_by_name("Clips/" + style + "/IteratorToStringHandle:0") for style in train_styles_list]
    frame_handle_phs = [graph.get_tensor_by_name("Frames/" + style + "/IteratorToStringHandle:0") for style in train_styles_list]

    nn_clip_seed = graph.get_tensor_by_name(name_clip_seed)
    nn_frame_seed = graph.get_tensor_by_name(name_frame_seed)
    nn_clip_handle = graph.get_tensor_by_name(name_clip_handle)
    nn_frame_handle = graph.get_tensor_by_name(name_frame_handle)
    nn_train_flag_0 = graph.get_tensor_by_name(name_train_flag_0)
    nn_train_flag_1 = graph.get_tensor_by_name(name_train_flag_1)
    nn_film_flag = graph.get_tensor_by_name(name_film_flag)
    nn_film_params = graph.get_tensor_by_name(name_film_params)

    # test_clips = graph.get_tensor_by_name("Clips/IteratorGetNext:0")
    test_frames = graph.get_tensor_by_name("Frames/IteratorGetNext:0")

    # Initializing datasets 
    clip_handles = [sess.run(handle_ph) for handle_ph in clip_handle_phs]
    frame_handles = [sess.run(handle_ph) for handle_ph in frame_handle_phs]

    train_dict = {}
    for i, style in enumerate(train_styles_list):
        """
        Extracts multiple sets of FiLM parameters per style from different input clips. 
	We save the first set of FiLM parameters as binaries for use with unity.
        We pickle all the FiLM parameters for examination with t-SNE.
        """

        sess.run(clip_init_iterators[i], feed_dict={nn_batch_size: num_codes, nn_mode: "Train", nn_clip_seed: 12345})
        sess.run(frame_init_iterators[i], feed_dict={nn_batch_size: num_codes, nn_mode: "Train", nn_frame_seed: 12345})

        # Creating output tensor
        output_tensor = graph.get_tensor_by_name(name_output_tensor)

        tensor_out = sess.run([output_tensor], feed_dict={nn_frame_handle: frame_handles[i], nn_clip_handle: clip_handles[i], nn_train_flag_0: False,
                                                        nn_train_flag_1: False, nn_film_flag: True, nn_film_params: np.ones((num_codes,512*4))})
        
        train_dict[style] = tensor_out[0]  # num_codes, 2048

    print("Train Values Extracted")

    for i, style in enumerate(train_styles_list):
        train_dict[style][0:1,:512].tofile(config["save_path"]+"Weights/Style_{:03}_scale1.bin".format(i))
        train_dict[style][0:1,512:512*2].tofile(config["save_path"]+"Weights/Style_{:03}_shift1.bin".format(i))
        train_dict[style][0:1,512*2:512*3].tofile(config["save_path"]+"Weights/Style_{:03}_scale2.bin".format(i))
        train_dict[style][0:1,512*3:].tofile(config["save_path"]+"Weights/Style_{:03}_shift2.bin".format(i))
    print("Binaries Saved For Single Set of FiLM Parameters")

    with open(config["save_path"] +"Weights/{}_FiLM_Parameters".format(num_codes), 'wb') as f:
        pickle.dump(train_dict, f)
    print("Pickled {} FiLM Parameters per style".format(num_codes))

    """ To compare different codes for the same style against one another """
    # for i, style in enumerate(train_styles_list):
    #     train_dict[style][1:2,:512].tofile(config["save_path"]+"Weights2/Style_{:03}_scale1.bin".format(i))
    #     train_dict[style][1:2,512:512*2].tofile(config["save_path"]+"Weights2/Style_{:03}_shift1.bin".format(i))
    #     train_dict[style][1:2,512*2:512*3].tofile(config["save_path"]+"Weights2/Style_{:03}_scale2.bin".format(i))
    #     train_dict[style][1:2,512*3:].tofile(config["save_path"]+"Weights2/Style_{:03}_shift2.bin".format(i))
    # print("Binaries Saved For Single Set of FiLM Parameters")

    # with open(config["save_path"] +"Weights2/{}_FiLM_Parameters".format(num_codes), 'wb') as f:
    #     pickle.dump(train_dict, f)
    # print("Pickled {} FiLM Parameters per style".format(num_codes))
