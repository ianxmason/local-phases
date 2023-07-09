import tensorflow as tf
import numpy as np
import argparse
import os
import yaml
"""
Save normalization statistics as binaries for use with unity
"""

with open('LPN_100Style25B_BLL.yml') as f: 
    config = yaml.load(f, Loader=yaml.FullLoader)

stats_path = config["stats_path"]
save_path = config["save_path"] + "Weights/"

with open(stats_path + "InputNorm.txt", 'r') as f:
    lines = f.readlines()
    X_mean = np.fromstring(lines[0], dtype=np.float32, sep=' ')[0:config["Xdim"]]  # dtype float32 is vital here - without it, doesn't work at all!
    X_std = np.fromstring(lines[1], dtype=np.float32, sep=' ')[0:config["Xdim"]]

    X_mean = np.concatenate([X_mean, np.zeros(config["one_hot_dim"], dtype=np.float32)])
    X_std = np.concatenate([X_std, np.ones(config["one_hot_dim"], dtype=np.float32)])

    assert config["Pdim"] == 8

    X_mean_gt = np.fromstring(lines[0], dtype=np.float32, sep=' ')[config["Xdim"]:config["Xdim"] + 8]
    X_std_gt = np.fromstring(lines[1], dtype=np.float32, sep=' ')[config["Xdim"]:config["Xdim"] + 8]

    for i in range(len(X_std)):
        if (X_std[i]==0):
            X_std[i]=1

    for i in range(len(X_std_gt)):
        if (X_std_gt[i]==0):
            X_std_gt[i]=1


with open(stats_path + "OutputNorm.txt", 'r') as f:
    lines = f.readlines()
    Y_mean = np.concatenate((np.fromstring(lines[0], dtype=np.float32, sep=' ')[0:config["Ydim"] - 2],
                             np.fromstring(lines[0], dtype=np.float32, sep=' ')[-2:]))
    Y_std = np.concatenate((np.fromstring(lines[1], dtype=np.float32, sep=' ')[0:config["Ydim"] - 2],
                            np.fromstring(lines[1], dtype=np.float32, sep=' ')[-2:]))
    

    for i in range(config["Ydim"]):
        if (Y_std[i]==0):
            Y_std[i]=1

X_mean.tofile(save_path + "Xmean.bin")
X_std.tofile(save_path + "Xstd.bin")
Y_mean.tofile(save_path + "Ymean.bin")
Y_std.tofile(save_path + "Ystd.bin")

X_mean_gt.tofile(save_path + "GT_Xmean.bin")
X_std_gt.tofile(save_path + "GT_Xstd.bin")

with open(save_path + "Xmean.bin") as f:
	in_arr = np.fromfile(f, dtype=np.float32)
	print(in_arr.shape)

with open(save_path + "GT_Xmean.bin") as f:
    in_arr = np.fromfile(f, dtype=np.float32)
    print(in_arr.shape)

			

