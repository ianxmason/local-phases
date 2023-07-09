import tensorflow as tf
import numpy as np
import argparse
import os
import csv
from collections import defaultdict
"""
Converts txt data outputted by unity processing to tfrecords format. One tfrecords file for each style.

To run:
python clip_tfrecords_noidle.py /path/to/data/ /path/to/save/TFRecords/ /path/to/Tr_Va_Te_Clips.txt 95 240
"""
parser = argparse.ArgumentParser(description='Processes trajectory files into TFRecord file to load into tensorflow')
parser.add_argument('data_path', help='path to trajectroy data txt folder')
parser.add_argument('save_path', help='path to folder for saving the TFRecord')
parser.add_argument('tr_va_te_path', help='path to txt file containing train/val/test frame splits')
parser.add_argument('num_styles', help='number of styles as an int') 
parser.add_argument('clip_size', type=int, help='length of clips to create for FiLM generator')
args = parser.parse_args()

def _float_feature(value):
  """Returns a float_list from a float / double."""
  return tf.train.Feature(float_list=tf.train.FloatList(value=value))

# For 5 style dataset. In alphabetical order - the same order as one hots.
# styles_list = ["Roadrunner", "ShieldedLeft", "Skip", "Star", "WildArms"]

# 95 style
styles_list = ["Aeroplane", "Akimbo", "Angry", "ArmsAboveHead", "ArmsBehindBack", "ArmsBySide", "ArmsFolded", "Balance",
                "BeatChest", "BentForward", "BentKnees", "BigSteps", "BouncyLeft", "BouncyRight", "Cat", "Chicken",
                "CrossOver", "Crouched", "CrowdAvoidance", "Depressed", "Dinosaur", "DragLeftLeg", "DragRightLeg",
                "Drunk", "DuckFoot", "Elated", "FairySteps", "Flapping", "FlickLegs", "Followed", "GracefulArms", "HandsBetweenLegs",
                "HandsInPockets", "Heavyset", "HighKnees", "InTheDark", "KarateChop", "Kick", "LawnMower", "LeanBack",
                "LeanLeft", "LeanRight", "LeftHop", "LegsApart", "LimpLeft", "LimpRight", "LookUp", "Lunge",
                "March", "Monk", "Morris", "Neutral", "Old", "OnHeels", "OnPhoneLeft", "OnPhoneRight", "OnToesBentForward",
                "OnToesCrouched", "PendulumHands", "Penguin", "PigeonToed", "Proud", "Punch", "Quail", "RaisedLeftArm",
                "RaisedRightArm", "RightHop", "Robot", "Rocket", "Rushed", "ShieldedRight",
                "SlideFeet", "SpinAntiClock", "SpinClock", "StartStop", "Stiff", "Strutting", "Superman",
                "Swat", "Sweep", "Swimming", "SwingArmsRound", "SwingShoulders", "Teapot", "Tiptoe", "TogetherStep",
                "TwoFootJump", "WalkingStickLeft", "WalkingStickRight", "Waving", "WhirlArms", "WideLegs", "WiggleHips",
                "WildLegs", "Zombie"]

# 100 style
# styles_list = ["Aeroplane", "Akimbo", "Angry", "ArmsAboveHead", "ArmsBehindBack", "ArmsBySide", "ArmsFolded", "Balance",
#                 "BeatChest", "BentForward", "BentKnees", "BigSteps", "BouncyLeft", "BouncyRight", "Cat", "Chicken",
#                 "CrossOver", "Crouched", "CrowdAvoidance", "Depressed", "Dinosaur", "DragLeftLeg", "DragRightLeg",
#                 "Drunk", "DuckFoot", "Elated", "FairySteps", "Flapping", "FlickLegs", "Followed", "GracefulArms", "HandsBetweenLegs",
#                 "HandsInPockets", "Heavyset", "HighKnees", "InTheDark", "KarateChop", "Kick", "LawnMower", "LeanBack",
#                 "LeanLeft", "LeanRight", "LeftHop", "LegsApart", "LimpLeft", "LimpRight", "LookUp", "Lunge",
#                 "March", "Monk", "Morris", "Neutral", "Old", "OnHeels", "OnPhoneLeft", "OnPhoneRight", "OnToesBentForward",
#                 "OnToesCrouched", "PendulumHands", "Penguin", "PigeonToed", "Proud", "Punch", "Quail", "RaisedLeftArm",
#                 "RaisedRightArm", "RightHop", "Roadrunner", "Robot", "Rocket", "Rushed", "ShieldedLeft", "ShieldedRight",
#                 "Skip", "SlideFeet", "SpinAntiClock", "SpinClock", "Star", "StartStop", "Stiff", "Strutting", "Superman",
#                 "Swat", "Sweep", "Swimming", "SwingArmsRound", "SwingShoulders", "Teapot", "Tiptoe", "TogetherStep",
#                 "TwoFootJump", "WalkingStickLeft", "WalkingStickRight", "Waving", "WhirlArms", "WideLegs", "WiggleHips",
#                 "WildArms", "WildLegs", "Zombie"]

styles_list.sort()

assert len(styles_list) == int(args.num_styles)

# Xdim = 385 # 384 features + phase
# Totaldim = 388 + int(args.num_styles)  # all features including one hot labels

# For 25 bone skeleton
Xdim = 349 # 348 features + phase
Totaldim = 352 + int(args.num_styles)  # all features including one hot labels

fname_list = []
for fname in os.listdir(args.data_path):
	if (fname.split('.')[0][0:5] == "Input") and (fname.split('.')[0][-10:] == "TrainClips" or fname.split('.')[0][-8:] == "ValClips" or fname.split('.')[0][-9:] == "TestClips"):
		fname_list.append(fname)
fname_list.sort()


# This has many repeated sweeps over the data but it works
# For writing to TFRecord
# Input - write all traj, joints and phase. Currently we do not write gait or style onehot. Style is in the file name
# Output - write all traj, joints and phase update. Currently we do not write gait or style onehot.
for fname in fname_list:	
	print("Writing from {}".format(fname))
	for i, style in enumerate(styles_list):
		written = False
		with open(args.data_path + fname, 'r') as f:
			if os.path.isfile(args.save_path + style + fname.split('.')[0] + '.tfrecords'):
				print("Already exists {}".format(style + fname.split('.')[0] + '.tfrecords'))
			else:
				with tf.python_io.TFRecordWriter(args.save_path + style + fname.split('.')[0] + '.tfrecords') as writer:
					for line in f:
						reshaped = np.fromstring(line, sep=' ').reshape((-1, Totaldim))
						assert reshaped.shape[0] == args.clip_size
						labels = reshaped[0,-int(args.num_styles):]	
						gait_labels = reshaped[0, -int(args.num_styles)-3: -int(args.num_styles)]			
						if int(labels[i]) == 1 and int(gait_labels[2]) != 1:
							example = tf.train.Example(
								features=tf.train.Features(
									feature={
										'features': _float_feature(reshaped[:,0:Xdim].flatten()),
									}))
							writer.write(example.SerializeToString())
							written = True
						elif int(float(labels[i])) != 1 and written:  # increases efficiency so don't continue to read the other styles once we have extracted all of one style.
							if written:
								break
							else:
								pass
		print("Style {} written".format(style))

print("Completed {} frame clips".format(args.clip_size))

