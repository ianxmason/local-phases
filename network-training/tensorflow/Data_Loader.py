import numpy as np
import tensorflow as tf


class Data_Loader(object):
	def __init__(self, config):
		self.config = config
		self.batch_size_ph = tf.placeholder(tf.int64, shape=[], name="batch_size_ph")
		self.clip_seed_ph = tf.placeholder(tf.int64, shape=[], name="clip_seed_ph")
		self.frame_seed_ph = tf.placeholder(tf.int64, shape=[], name="frame_seed_ph")
		self.mode_ph = tf.placeholder(tf.string, shape=[], name="mode_ph")  # Train/Val/Test

		with open(self.config["stats_path"] + "InputNorm.txt", 'r') as f:
			lines = f.readlines()

			# Load joint pos/vel/rot statistics
			mu = np.fromstring(lines[0], dtype=np.float32, sep=' ')[0:self.config["Xdim"]]  # dtype float32 is vital here - without it, doesn't work at all!
			sig = np.fromstring(lines[1], dtype=np.float32, sep=' ')[0:self.config["Xdim"]]
			# For one hot vectors add 0 mean and 1 std
			mu = np.concatenate([mu, np.zeros(self.config["one_hot_dim"], dtype=np.float32)])
			sig = np.concatenate([sig, np.ones(self.config["one_hot_dim"], dtype=np.float32)])
			# Add phase statistics if required
			if self.config["Pdim"] == 0:
				pass
			elif self.config["Pdim"] == 1:
				# In the PFNN we do not want to normalise the phase (as it is used in the spline, not as an input to a network)
				mu = np.concatenate([mu, np.zeros(1, dtype=np.float32)])
				sig = np.concatenate([sig, np.ones(1, dtype=np.float32)])
			elif self.config["Pdim"] == 8:
				mu = np.concatenate([mu, np.fromstring(lines[0], dtype=np.float32, sep=' ')[self.config["Xdim"]:self.config["Xdim"] + 8]])
				sig = np.concatenate([sig, np.fromstring(lines[1], dtype=np.float32, sep=' ')[self.config["Xdim"]:self.config["Xdim"] + 8]])
			else:
				raise ValueError("Pdim must be 0 (MANN), 1 (PFNN) or 8 (LPN)")
			# Avoid dividing by zero
			for i in range(len(sig)):
				if (sig[i]==0):
					sig[i]=1
			self.in_mean = tf.constant(mu, dtype=tf.float32)
			self.in_std = tf.constant(sig, dtype=tf.float32)

		# subtract 2 from Ydim as footcontacts are at the end after the global phase, gait labels and one hot labels
		with open(self.config["stats_path"] + "OutputNorm.txt", 'r') as f:
			lines = f.readlines()
			if self.config["Pdim"] != 1:
				mu = np.concatenate((np.fromstring(lines[0], dtype=np.float32, sep=' ')[0:self.config["Ydim"] - 2],
					                 np.fromstring(lines[0], dtype=np.float32, sep=' ')[-2:]))
				sig = np.concatenate((np.fromstring(lines[1], dtype=np.float32, sep=' ')[0:self.config["Ydim"] - 2],
					                  np.fromstring(lines[1], dtype=np.float32, sep=' ')[-2:]))
			else: # when Pdim == 1 we are using a global phase value.
				# Because OutputNorm contains mean/std for local phases as well, we need to get the global phase value 
				# from the correct index
				mu = np.concatenate((np.fromstring(lines[0], dtype=np.float32, sep=' ')[0:self.config["Ydim"] - 1 - 2],  # -1 phase update, -2 foot contacts
					                 np.fromstring(lines[0], dtype=np.float32, sep=' ')[self.config["Ydim"] - 3 + 17:self.config["Ydim"] - 3 + 18],
					                 np.fromstring(lines[0], dtype=np.float32, sep=' ')[-2:]))
				sig = np.concatenate((np.fromstring(lines[1], dtype=np.float32, sep=' ')[0:self.config["Ydim"] - 1 - 2],
					                  np.fromstring(lines[1], dtype=np.float32, sep=' ')[self.config["Ydim"] - 3 + 17:self.config["Ydim"] - 3 + 18],
					                  np.fromstring(lines[1], dtype=np.float32, sep=' ')[-2:]))
			for i in range(self.config["Ydim"]):
				if (sig[i]==0):
					sig[i]=1
			self.out_mean = tf.constant(mu, dtype=tf.float32)
			self.out_std = tf.constant(sig, dtype=tf.float32)


	def _input_parse_frames(self, example_proto): 
		# features = {"features": tf.FixedLenFeature([self.config["Xdim"] + self.config["frame_gait"] + 1], tf.float32)} # includes phase
		features = {"features": tf.FixedLenFeature([self.config["Xdim"] + self.config["one_hot_dim"] + self.config["Pdim"]], tf.float32)} # includes local phase
		parsed_features = tf.parse_single_example(example_proto, features)
		return parsed_features["features"]

	def _input_normalise_frames(self, features):
		features = (features-self.in_mean)/(self.in_std) 
		return features

	def _input_parse_clips(self, example_proto): 
		features = {"features": tf.FixedLenFeature([self.config["clip_size"]*(self.config["Xdim"]+1)], tf.float32)} # includes phase but not gait label
		parsed_features = tf.parse_single_example(example_proto, features)
		return parsed_features["features"]

	def _input_reshape_clips(self, features):
		# Reshapes and trims clips (i.e. removes phase and trajectory)
		if self.config["use_clip_trajectory"]:
			reshaped = tf.reshape(features, (self.config["clip_size"], self.config["Xdim"]+1))[:,0:self.config["Xdim"]]
		else:
			reshaped = tf.reshape(features, (self.config["clip_size"], self.config["Xdim"]+1))[:,48:self.config["Xdim"]]  # This removes trajectory and phase from FiLM generator
		return reshaped

	def _input_normalise_clips(self, features):
		# Cut trajectory and phase from mean/std and normalise
		if self.config["Pdim"] != 0:
			if self.config["use_clip_trajectory"]:
				features = (features-self.in_mean[0:-self.config["Pdim"]])/(self.in_std[0:-self.config["Pdim"]])
			else: 
				features = (features-self.in_mean[48:-self.config["Pdim"]])/(self.in_std[48:-self.config["Pdim"]])
		else:
			if self.config["use_clip_trajectory"]:
				features = (features-self.in_mean[0:])/(self.in_std[0:])
			else: 
				features = (features-self.in_mean[48:])/(self.in_std[48:])

		return features

	def _output_parse_frames(self, example_proto): 
		features = {"features": tf.FixedLenFeature([self.config["Ydim"]], tf.float32)} 
		parsed_features = tf.parse_single_example(example_proto, features)
		return parsed_features["features"]

	def _output_normalise_frames(self, features):
		features = (features-self.out_mean)/(self.out_std) 
		return features

	def load_frames(self):
		# Get alphabetical list of tfrecord files, assumes certain directory structure and naming conventions.
		styles_list = self.config["styles_list"]
		styles_list.sort()

		input_fname_frame=[]
		output_fname_frame=[]

		tf_data_path = tf.constant(self.config["data_path"], dtype=tf.string)
		tf_input = tf.constant("Input", dtype=tf.string)
		tf_output = tf.constant("Output", dtype=tf.string)
		tf_frames = tf.constant("Frames.tfrecords", dtype=tf.string)

		for style in styles_list:    
			tf_style = tf.constant(style, dtype=tf.string)
			tf_fname_in_frame = tf.strings.join([tf_data_path, tf_style, tf_input, self.mode_ph, tf_frames])
			input_fname_frame.append(tf_fname_in_frame)
			tf_fname_out_frame = tf.strings.join([tf_data_path, tf_style, tf_output, self.mode_ph, tf_frames])
			output_fname_frame.append(tf_fname_out_frame)


		# Make one dataset for each style
		# Input is next_frame[0], output in next_frame[1]
		frame_datasets = []
		for input_fname, output_fname in zip(input_fname_frame, output_fname_frame):
			input_dataset = tf.data.TFRecordDataset(filenames=input_fname)
			output_dataset = tf.data.TFRecordDataset(filenames=output_fname)
			input_dataset = input_dataset.map(self._input_parse_frames) # extracts from TFRecord format
			output_dataset = output_dataset.map(self._output_parse_frames)
			input_dataset = input_dataset.map(self._input_normalise_frames)
			output_dataset = output_dataset.map(self._output_normalise_frames)
			# input_dataset = input_dataset.map(self._input_phase_to_end_frames)  # As long as we have no gait labels we don't need to rewrite this for local phases
			# if not self.config["frame_vel"]:
			# 	input_dataset = input_dataset.map(self._input_remove_vel)
			# 	output_dataset = output_dataset.map(self._output_remove_vel)
			in_out_dataset = tf.data.Dataset.zip((input_dataset, output_dataset))
			in_out_dataset = in_out_dataset.shuffle(buffer_size=1000, seed=self.frame_seed_ph, reshuffle_each_iteration=True)
			in_out_dataset = in_out_dataset.batch(self.batch_size_ph, drop_remainder=True)
			frame_datasets.append(in_out_dataset)
		

		"""We use feedable iterators to allow us to switch between datasets while picking up where we left off"""
		with tf.variable_scope("Frames"):
			frame_handle_ph = tf.placeholder(tf.string, shape=[], name="frame_handle_ph")
			frame_iterator = tf.data.Iterator.from_string_handle(frame_handle_ph, frame_datasets[0].output_types, frame_datasets[0].output_shapes)
			next_frame = frame_iterator.get_next()
			frame_init_iterators = []
			for i, dataset in enumerate(frame_datasets):
				with tf.variable_scope(styles_list[i]):
					frame_init_iterators.append(dataset.make_initializable_iterator())

		return next_frame, frame_handle_ph, frame_init_iterators


	def load_clips(self):
		# Get alphabetical list of tfrecord files, assumes certain directory structure and naming conventions.
		styles_list = self.config["styles_list"]
		styles_list.sort()  # Ensure alphabetical

		input_fname_clip=[]

		tf_data_path = tf.constant(self.config["data_path"], dtype=tf.string)
		tf_input = tf.constant("Input", dtype=tf.string)
		tf_clips = tf.constant("Clips.tfrecords", dtype=tf.string)

		for style in styles_list:    
			tf_style = tf.constant(style, dtype=tf.string)
			tf_fname_clip = tf.strings.join([tf_data_path, tf_style, tf_input, self.mode_ph, tf_clips])
			input_fname_clip.append(tf_fname_clip)


		clip_datasets = []
		for input_fname in input_fname_clip:
			input_dataset = tf.data.TFRecordDataset(filenames=input_fname)
			input_dataset = input_dataset.map(self._input_parse_clips)
			# Take only one clip from the available ones
			# input_dataset = input_dataset.take(1)
			input_dataset = input_dataset.map(self._input_reshape_clips) 
			input_dataset = input_dataset.map(self._input_normalise_clips)
			# if not self.config["clip_vel"]:
			# 	input_dataset = input_dataset.map(self._input_remove_vel_clips)
			# Reduce buffer size from 500 to 50 for better memory use
			input_dataset = input_dataset.shuffle(buffer_size=50, seed=self.clip_seed_ph, reshuffle_each_iteration=True)
			input_dataset = input_dataset.repeat()  # allows clip dataset to repeat indefinitely
			input_dataset = input_dataset.batch(self.batch_size_ph)
			clip_datasets.append(input_dataset)  

		"""We use feedable iterators to allow us to switch between datasets while picking up where we left off"""
		with tf.variable_scope("Clips"):
			clip_handle_ph = tf.placeholder(tf.string, shape=[], name="clip_handle_ph")
			clip_iterator = tf.data.Iterator.from_string_handle(clip_handle_ph, clip_datasets[0].output_types, clip_datasets[0].output_shapes)
			next_clip = clip_iterator.get_next()
			clip_init_iterators = []
			for i, dataset in enumerate(clip_datasets):
				with tf.variable_scope(styles_list[i]):
					clip_init_iterators.append(dataset.make_initializable_iterator())

		return next_clip, clip_handle_ph, clip_init_iterators

