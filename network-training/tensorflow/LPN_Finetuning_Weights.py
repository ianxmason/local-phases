import numpy as np
import tensorflow as tf
import yaml
from ExpertWeights import save_EP
from Gating import save_GT
from tensorflow.python.saved_model import tag_constants

def get_finetuning_weights(config):
    with tf.Session() as FTsess:
        tf.saved_model.loader.load(FTsess, [tag_constants.SERVING], config["ft_model_path"])
        graph = tf.get_default_graph()

        # Necessary pre-initialisation
        styles_list = config["styles_list"]  # for binary exporting
        # styles_list = config["styles_list_ft"]  # for finetuning training
        styles_list.sort()
        name_batch_size_tensor = config["ft_batch_size"]
        name_mode_tensor = config["ft_mode"]
        name_clip_seed = config["ft_clip_seed"]
        name_frame_seed = config["ft_frame_seed"]
        name_clip_handle = config["ft_clip_handle"]
        name_frame_handle = config["ft_frame_handle"]

        nn_batch_size = graph.get_tensor_by_name(name_batch_size_tensor)
        nn_mode = graph.get_tensor_by_name(name_mode_tensor)
        clip_init_iterators_FT = [graph.get_operation_by_name("Clips/" + style + "/MakeIterator") for style in styles_list]
        frame_init_iterators_FT = [graph.get_operation_by_name("Frames/" + style + "/MakeIterator") for style in styles_list]
        clip_handle_phs_FT = [graph.get_tensor_by_name("Clips/" + style + "/IteratorToStringHandle:0") for style in styles_list]
        frame_handle_phs_FT = [graph.get_tensor_by_name("Frames/" + style + "/IteratorToStringHandle:0") for style in styles_list]
        nn_clip_seed = graph.get_tensor_by_name(name_clip_seed)
        nn_frame_seed = graph.get_tensor_by_name(name_frame_seed)
        nn_clip_handle = graph.get_tensor_by_name(name_clip_handle)
        nn_frame_handle = graph.get_tensor_by_name(name_frame_handle)

        clip_handles_FT = [FTsess.run(handle_ph) for handle_ph in clip_handle_phs_FT]
        frame_handles_FT = [FTsess.run(handle_ph) for handle_ph in frame_handle_phs_FT]

        FiLM_tensors = [graph.get_tensor_by_name(nm) for nm in config["FiLM_ft_weights"]]
        GT_tensors = [graph.get_tensor_by_name(nm) for nm in config["GT_weights"]]
        EP_tensors = [graph.get_tensor_by_name(nm) for nm in config["EP_weights"]]
        FiLM_Weights, GT_Weights, EP_Weights = FTsess.run([FiLM_tensors, GT_tensors, EP_tensors], feed_dict={nn_frame_handle: frame_handles_FT[0], nn_clip_handle: clip_handles_FT[0]})

    tf.reset_default_graph()  # Don't want to keep old graph for new style
    return FiLM_Weights, GT_Weights, EP_Weights

if __name__ == "__main__":
    # We can also use this function to get PFNN weights as a binary to run in unity

    with open('LPN_100Style25B_BLL.yml') as f:    
        config = yaml.load(f, Loader=yaml.FullLoader)

    FiLM_Weights, GT_Weights, EP_Weights = get_finetuning_weights(config)

    weights = (GT_Weights[0], GT_Weights[2], GT_Weights[4])
    biases = (GT_Weights[1], GT_Weights[3], GT_Weights[5])
    save_GT(weights, biases, config["save_path"]+"Weights")

    alphas = (EP_Weights[0],EP_Weights[2],EP_Weights[4])
    betas = (EP_Weights[1],EP_Weights[3],EP_Weights[5])
    save_EP(alphas, betas, config["save_path"]+"Weights", config["num_experts"])
