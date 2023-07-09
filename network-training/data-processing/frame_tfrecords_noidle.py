import tensorflow as tf
import numpy as np
import argparse
import os
"""
Converts txt data outputted by unity processing to tfrecords format. One tfrecords file for each style.

To run:
python frame_tfrecords_noidle.py /path/to/data/ /path/to/save/TFRecords/ 100 
"""
parser = argparse.ArgumentParser(description='Processes trajectory files into TFRecord file to load into tensorflow')
parser.add_argument('data_path', help='path to trajectory data txt folder')
parser.add_argument('save_path', help='path to folder for saving the TFRecord')
parser.add_argument('num_styles', help='number of styles as an int') 
args = parser.parse_args()

def _float_feature(value):
  """Returns a float_list from a float / double."""
  return tf.train.Feature(float_list=tf.train.FloatList(value=value))

# For 5 style dataset. In alphabetical order - the same order as one hots.
# styles_list = ["Roadrunner", "ShieldedLeft", "Skip", "Star", "WildArms"]

# 95 style
# styles_list = ["Aeroplane", "Akimbo", "Angry", "ArmsAboveHead", "ArmsBehindBack", "ArmsBySide", "ArmsFolded", "Balance",
#                 "BeatChest", "BentForward", "BentKnees", "BigSteps", "BouncyLeft", "BouncyRight", "Cat", "Chicken",
#                 "CrossOver", "Crouched", "CrowdAvoidance", "Depressed", "Dinosaur", "DragLeftLeg", "DragRightLeg",
#                 "Drunk", "DuckFoot", "Elated", "FairySteps", "Flapping", "FlickLegs", "Followed", "GracefulArms", "HandsBetweenLegs",
#                 "HandsInPockets", "Heavyset", "HighKnees", "InTheDark", "KarateChop", "Kick", "LawnMower", "LeanBack",
#                 "LeanLeft", "LeanRight", "LeftHop", "LegsApart", "LimpLeft", "LimpRight", "LookUp", "Lunge",
#                 "March", "Monk", "Morris", "Neutral", "Old", "OnHeels", "OnPhoneLeft", "OnPhoneRight", "OnToesBentForward",
#                 "OnToesCrouched", "PendulumHands", "Penguin", "PigeonToed", "Proud", "Punch", "Quail", "RaisedLeftArm",
#                 "RaisedRightArm", "RightHop", "Robot", "Rocket", "Rushed", "ShieldedRight",
#                 "SlideFeet", "SpinAntiClock", "SpinClock", "StartStop", "Stiff", "Strutting", "Superman",
#                 "Swat", "Sweep", "Swimming", "SwingArmsRound", "SwingShoulders", "Teapot", "Tiptoe", "TogetherStep",
#                 "TwoFootJump", "WalkingStickLeft", "WalkingStickRight", "Waving", "WhirlArms", "WideLegs", "WiggleHips",
#                 "WildLegs", "Zombie"]

# 100 style
styles_list = ["Aeroplane", "Akimbo", "Angry", "ArmsAboveHead", "ArmsBehindBack", "ArmsBySide", "ArmsFolded", "Balance",
                "BeatChest", "BentForward", "BentKnees", "BigSteps", "BouncyLeft", "BouncyRight", "Cat", "Chicken",
                "CrossOver", "Crouched", "CrowdAvoidance", "Depressed", "Dinosaur", "DragLeftLeg", "DragRightLeg",
                "Drunk", "DuckFoot", "Elated", "FairySteps", "Flapping", "FlickLegs", "Followed", "GracefulArms", "HandsBetweenLegs",
                "HandsInPockets", "Heavyset", "HighKnees", "InTheDark", "KarateChop", "Kick", "LawnMower", "LeanBack",
                "LeanLeft", "LeanRight", "LeftHop", "LegsApart", "LimpLeft", "LimpRight", "LookUp", "Lunge",
                "March", "Monk", "Morris", "Neutral", "Old", "OnHeels", "OnPhoneLeft", "OnPhoneRight", "OnToesBentForward",
                "OnToesCrouched", "PendulumHands", "Penguin", "PigeonToed", "Proud", "Punch", "Quail", "RaisedLeftArm",
                "RaisedRightArm", "RightHop", "Roadrunner", "Robot", "Rocket", "Rushed", "ShieldedLeft", "ShieldedRight",
                "Skip", "SlideFeet", "SpinAntiClock", "SpinClock", "Star", "StartStop", "Stiff", "Strutting", "Superman",
                "Swat", "Sweep", "Swimming", "SwingArmsRound", "SwingShoulders", "Teapot", "Tiptoe", "TogetherStep",
                "TwoFootJump", "WalkingStickLeft", "WalkingStickRight", "Waving", "WhirlArms", "WideLegs", "WiggleHips",
                "WildArms", "WildLegs", "Zombie"]

styles_list.sort()

assert len(styles_list) == int(args.num_styles)

fname_list = []
for fname in os.listdir(args.data_path):
	if fname.split('.')[0][-5:] == "Train" or fname.split('.')[0][-3:] == "Val" or fname.split('.')[0][-4:] == "Test":
		fname_list.append(fname)
fname_list.sort()

# This has many repeated sweeps over the data but it works
# For writing to TFRecord
# Input - write all traj, joints and phase. Currently we do not write gait or style onehot. Style is in the file name
# Output - write all traj, joints and phase update. Currently we do not write gait or style onehot.


"""
With foot contacts
"""
Xdim = 356 # 348 motionfeatures + 8 local phases + 0 gait labels
Ydim = 340 # 324 motionfeatures + 8 next local phases + 8 local phase updates  (foot contacts added in later)
for fname in fname_list:	
	print("Writing from {}".format(fname))
	for i, style in enumerate(styles_list):
		written = False
		with open(args.data_path + fname, 'r') as f:
			if os.path.isfile(args.save_path + style + fname.split('.')[0] + 'Frames.tfrecords'):
				print("Already exists {}".format(style + fname.split('.')[0] + 'Frames.tfrecords'))
			else:
				with tf.python_io.TFRecordWriter(args.save_path + style + fname.split('.')[0] + 'Frames.tfrecords') as writer:
					for line in f:
						if fname.split('.')[0][0:5] == "Input":
							labels = line.split(' ')[-int(args.num_styles):]
							gait_labels = line.split(' ')[-int(args.num_styles)-3: -int(args.num_styles)]
						else:
							labels = line.split(' ')[-int(args.num_styles)-2:-2]
							gait_labels = line.split(' ')[-int(args.num_styles)-5: -int(args.num_styles)-2]
							foot_contacts = line.split(' ')[-2:]
						if int(float(labels[i])) == 1 and int(float(gait_labels[2])) != 1:
							if fname.split('.')[0][0:5] == "Input":
								example = tf.train.Example(
									features=tf.train.Features(
										feature={
											'features': _float_feature(np.fromstring(line, sep=' ')[0:Xdim]),
										}))
							elif fname.split('.')[0][0:6] == "Output":
								output_feature = np.concatenate((np.fromstring(line, sep=' ')[0:Ydim], np.fromstring(line, sep=' ')[-2:]))
								example = tf.train.Example(
									features=tf.train.Features(
										feature={
											'features': _float_feature(output_feature),
										}))
							else: 
								raise ValueError("Incorrect filenames in data directory")
							writer.write(example.SerializeToString())
							written = True
						elif int(float(labels[i])) != 1 and written:
							if written:
								break
							else:
								pass							
		print("Style {} written".format(style))

print("Completed.")



"""
With one hot labels and foot contacts
"""
# Xdim = 356 # 348 motionfeatures + 8 local phases + 0 gait labels
# Ydim = 340 # 324 motionfeatures + 8 next local phases + 8 local phase updates  (foot contacts added in later)
# for fname in fname_list:	
# 	print("Writing from {}".format(fname))
# 	for i, style in enumerate(styles_list):
# 		written = False
# 		with open(args.data_path + fname, 'r') as f:
# 			if os.path.isfile(args.save_path + style + fname.split('.')[0] + 'Frames.tfrecords'):
# 				print("Already exists {}".format(style + fname.split('.')[0] + 'Frames.tfrecords'))
# 			else:
# 				with tf.python_io.TFRecordWriter(args.save_path + style + fname.split('.')[0] + 'Frames.tfrecords') as writer:
# 					for line in f:
# 						if fname.split('.')[0][0:5] == "Input":
# 							labels = line.split(' ')[-int(args.num_styles):]
# 							gait_labels = line.split(' ')[-int(args.num_styles)-3: -int(args.num_styles)]
# 						else:
# 							labels = line.split(' ')[-int(args.num_styles)-2:-2]
# 							gait_labels = line.split(' ')[-int(args.num_styles)-5: -int(args.num_styles)-2]
# 							foot_contacts = line.split(' ')[-2:]
# 						if int(float(labels[i])) == 1 and int(float(gait_labels[2])) != 1:
# 							if fname.split('.')[0][0:5] == "Input":
# 								# Original set up
# 								# input_feature = np.concatenate((np.fromstring(line, sep=' ')[0:Xdim-1],
# 								# 								np.fromstring(line, sep=' ')[-int(args.num_styles):],
# 								# 								np.fromstring(line, sep=' ')[Xdim-1:Xdim]))  # phase at the end
# 								# example = tf.train.Example(
# 								# 	features=tf.train.Features(
# 								# 		feature={
# 								# 			'features': _float_feature(input_feature),
# 								# 		}))

# 								# Since moving the global phase we can now use this to export the local phase. To export the global phase we need 
# 								# to do something else - that is cut out the local phases. Move local phase to the end.
# 								input_feature = np.concatenate((np.fromstring(line, sep=' ')[0:Xdim-8],
# 																np.fromstring(line, sep=' ')[-int(args.num_styles):],
# 																np.fromstring(line, sep=' ')[Xdim-8:Xdim]))  # local phases at the end
# 								example = tf.train.Example(
# 									features=tf.train.Features(
# 										feature={
# 											'features': _float_feature(input_feature),
# 										}))

# 								# For old version adding in 8 local phases we also want to remove the original phase label so use the below
# 								# input_feature = np.concatenate((np.fromstring(line, sep=' ')[0:Xdim-8], np.fromstring(line, sep=' ')[Xdim-7:Xdim+1]))

# 								# input_feature = np.concatenate((np.fromstring(line, sep=' ')[0:Xdim-8],
# 								# 								np.fromstring(line, sep=' ')[-int(args.num_styles):],
# 								# 								np.fromstring(line, sep=' ')[Xdim-7:Xdim+1]))  # local phases at the end

# 								# example = tf.train.Example(
# 								# 	features=tf.train.Features(
# 								# 		feature={
# 								# 			'features': _float_feature(input_feature),
# 								# 		}))

# 							elif fname.split('.')[0][0:6] == "Output":
# 								# Since moving the global phase we can now use this to export the local phase. To export the global phase we need 
# 								# to do something else - that is cut out the local phases.
# 								output_feature = np.concatenate((np.fromstring(line, sep=' ')[0:Ydim], np.fromstring(line, sep=' ')[-2:]))
# 								example = tf.train.Example(
# 									features=tf.train.Features(
# 										feature={
# 											'features': _float_feature(output_feature),
# 										}))

# 								# For adding in 8 local phases and 8 updates we also want to remove the original global phase update so use the below
# 								# output_feature = np.concatenate((np.fromstring(line, sep=' ')[0:Ydim-16], np.fromstring(line, sep=' ')[Ydim-15:Ydim+1], np.fromstring(line, sep=' ')[-2:]))
# 								# example = tf.train.Example(
# 								# 	features=tf.train.Features(
# 								# 		feature={
# 								# 			'features': _float_feature(output_feature),
# 								# 		}))
# 							else: 
# 								raise ValueError("Incorrect filenames in data directory")
# 							writer.write(example.SerializeToString())
# 							written = True
# 						elif int(float(labels[i])) != 1 and written:
# 							if written:
# 								break
# 							else:
# 								pass							
# 		print("Style {} written".format(style))

# print("Completed.")
