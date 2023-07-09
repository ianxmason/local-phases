import numpy as np
import argparse

parser = argparse.ArgumentParser(description="Takes txt dataset files and outputs mean and std of the dataset, per element")
parser.add_argument('data_path', help="Path to data txt, each line is one datapoint")
parser.add_argument('out_path', help="Path to output txt, where mean and std will be stored")
args = parser.parse_args()

"""
data_path: /path/to/InputTrain.txt
out_path: /save/path/InputNorm.txt

We use naive biased calculation for the variance. 

For speed to avoid rewriting for adding footcontacts we normalise the whole output txt and edit the vector during training to remove
the gait and style labels. (In the local phase case we also delete the phase/phase update entries). Basically the things that get trimmed out in the TFRecords.
"""

"""
WARNING: if using output files with foot contacts at the end need to manually and -2 to the slicing to get the style labels
Otherwise the wrong styles will be missed out. But for inputs need to not do this.
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
                "HandsInPockets", "Heavyset", "HighKnees", "InTheDark", "KarateChop", "Kick", "LawnMower", "LeanBack",
                "LeanLeft", "LeanRight", "LeftHop", "LegsApart", "LimpLeft", "LimpRight", "LookUp", "Lunge",
                "March", "Monk", "Morris", "Neutral", "Old", "OnHeels", "OnPhoneLeft", "OnPhoneRight", "OnToesBentForward",
                "OnToesCrouched", "PendulumHands", "Penguin", "PigeonToed", "Proud", "Punch", "Quail", "RaisedLeftArm",
                "RaisedRightArm", "RightHop", "Robot", "Rocket", "Rushed", "ShieldedRight",
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
line_count=0
with open(args.data_path, 'rt') as f:
	for line in f:
		string_features = line.split(" ")
		features = np.asarray(string_features, dtype=np.float32)
		labels = features[-len(all_styles):].astype(np.int64)
		# labels = features[-len(all_styles)-2:-2].astype(np.int64)  # For footcontacts - that is for the outputs (but not the inputs!)	
		if np.any(labels[skip_indices]==1):
			continue
		if first_line:
			ft_sum = features
			ft_sum_sq = features ** 2
			line_count += 1
			first_line = False
		else:
			ft_sum += features
			ft_sum_sq += (features ** 2)
			line_count += 1

ft_mean = ft_sum / float(line_count)
ft_std = (ft_sum_sq / float(line_count)) - (ft_mean ** 2)
print(ft_std[np.where(ft_std < 0)])
print(np.where(ft_std < 0)) # Check these are just floating point errors and not big negative numbers
ft_std[np.where(ft_std < 0)] = 0 # 1e-5
ft_std = np.sqrt(ft_std)

ft_mean = np.around(ft_mean, decimals=5) # round to 5 dp
ft_std = np.around(ft_std, decimals=5)

str_mean = separator.join(ft_mean.astype(str).tolist())
str_std = separator.join(ft_std.astype(str).tolist())

with open(args.out_path, 'w+t') as f:
	f.write(str_mean +'\n')
	f.write(str_std)

print("Written mean and std.")
print("Total datapoints = {}".format(line_count))
print("Dimensionality = {}".format(len(ft_mean)))
