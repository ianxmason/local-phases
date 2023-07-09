import numpy as np
import argparse
from collections import defaultdict

parser = argparse.ArgumentParser(description="Takes txt dataset outputs average walking and running velocities per style")
parser.add_argument('data_path', help="Path to data txt including filename, each line is one datapoint")
parser.add_argument('out_path', help="Path where output binaries will be saved")
args = parser.parse_args()

"""
In unity we have a bias per style, roughly this should be the difference between consecutive trajectory points.
We use the final two trajectory points because this is how I think the implementation works (smoothly blending with prediction
and user input up to the final points where it is all input).

This code allows us to extract a reasonable setting for each style without manual tuning.

We have need only a subset of the styles in OutputTrain.txt, the two lists of styles with allows us to do this.

Some hardcoding of dimensions in this file requires use of an OutputTrain file with future trajectory as first 24 features

Example command
python Extract_Style_Velocities.py /path/to/OutputTrain.txt /save/path/
"""
all_styles = ["Aeroplane", "Akimbo", "Angry", "ArmsAboveHead", "ArmsBehindBack", "ArmsBySide", "ArmsFolded", "Balance",
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

styles_list = ["Aeroplane", "Akimbo", "Angry", "ArmsAboveHead", "ArmsBehindBack", "ArmsBySide", "ArmsFolded", "Balance",
		"BeatChest", "BentForward", "BentKnees", "BigSteps", "BouncyLeft", "BouncyRight", "Cat", "Chicken",
		"CrossOver", "Crouched", "CrowdAvoidance", "Depressed", "Dinosaur", "DragLeftLeg", "DragRightLeg",
		"Drunk", "DuckFoot", "Elated", "FairySteps", "Flapping", "FlickLegs", "Followed", "GracefulArms", "HandsBetweenLegs",
		"Heavyset", "HighKnees", "InTheDark", "KarateChop", "Kick", "LawnMower", "LeanBack",
		"LeanLeft", "LeanRight", "LeftHop", "LegsApart", "LimpLeft", "LimpRight", "LookUp", "Lunge",
		"March", "Monk", "Morris", "Neutral", "Old", "OnHeels", "OnPhoneLeft", "OnPhoneRight", "OnToesBentForward",
		"OnToesCrouched", "PendulumHands", "Penguin", "PigeonToed", "Proud", "Punch", "Quail", "RaisedLeftArm",
		"RaisedRightArm", "RightHop", "Robot", "Rocket", "Rushed", "ShieldedLeft", "ShieldedRight",
		"SlideFeet", "SpinAntiClock", "SpinClock", "StartStop", "Stiff", "Strutting", "Superman",
		"Swat", "Sweep", "Swimming", "SwingArmsRound", "SwingShoulders", "Teapot", "Tiptoe", "TogetherStep",
		"TwoFootJump", "WalkingStickLeft", "WalkingStickRight", "Waving", "WhirlArms", "WideLegs", "WiggleHips",
		"WildLegs", "Zombie"]  # 95 Styles

skip_indices = []
for i, s in enumerate(all_styles):
	if s not in styles_list:
		skip_indices.append(i)


separator = " "
first_line = True
walk_line_counts = defaultdict(float)
walk_velocities = defaultdict(float)
run_line_counts = defaultdict(float)
run_velocities = defaultdict(float)
with open(args.data_path, 'rt') as f:
	for line in f:
		string_features = line.split(" ")
		features = np.asarray(string_features, dtype=np.float32)
		labels = features[-len(all_styles)-2:-2].astype(np.int64)  # -2 for foot contact labels
		gait_labels = features[-len(all_styles)-5: -len(all_styles)-2]

		if np.any(labels[skip_indices]==1):
			continue

		cur_style_idx = np.where(labels==1)[0][0]
		x_start_pos = features[16]
		z_start_pos = features[17]
		x_end_pos = features[20]
		z_end_pos = features[21]
		cur_vel = np.sqrt((x_end_pos - x_start_pos) ** 2 + (z_end_pos - z_start_pos) ** 2)
		
		if gait_labels[0] == 1:
			walk_velocities[all_styles[cur_style_idx]] += cur_vel * 60  # 60 fps so * 60 to get metres/second
			walk_line_counts[all_styles[cur_style_idx]] += 1
		elif gait_labels[1] == 1:
			run_velocities[all_styles[cur_style_idx]] += cur_vel * 60
			run_line_counts[all_styles[cur_style_idx]] += 1
		

for k in walk_velocities.keys():
	walk_velocities[k] /= walk_line_counts[k]
	run_velocities[k] /= run_line_counts[k]

print(walk_velocities)
print(run_velocities)

walk_velocities_for_binary = np.zeros(len(styles_list), dtype=np.float32)
run_velocities_for_binary = np.zeros(len(styles_list), dtype=np.float32)

for i, s in enumerate(styles_list):
	walk_velocities_for_binary[i] = walk_velocities[s]
	run_velocities_for_binary[i] = run_velocities[s]

walk_velocities_for_binary.tofile(args.out_path + "Walk_Velocities.bin")
run_velocities_for_binary.tofile(args.out_path + "Run_Velocities.bin")

print("Written Velocities")
