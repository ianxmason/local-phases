import tensorflow as tf
import numpy as np

class Conv1D_Dense(object):
	"""
	1D convolutional network with final dense layers
	"""
	def __init__(self, num_conv_layers, filters, num_dense_layers, units, dropout_rate, max_pooling=None, padding="SAME", activation=tf.nn.relu, vscope="Conv1D_Dense", reuse=None):
		self.num_conv_layers = num_conv_layers
		self.num_dense_layers = num_dense_layers
		
		if len(filters)!=num_conv_layers or not all([isinstance(i,list) for i in filters]) or not all([len(i)==2 for i in filters]):
			raise ValueError("filters should be length num_conv_layers and contain lists of length 2 (num_filters, kernel_size)")
		else:
			self.filters = filters

		if len(units)!=num_dense_layers or not all([isinstance(i,list) for i in units]) or not all([len(i)==2 for i in units]):
			raise ValueError("units should be length num_dense_layers and contain lists of length 2")
		else:
			self.units = units

		if type(dropout_rate) == int or type(dropout_rate) == float:
			self.dropout_rate = [float(dropout_rate)] * (num_dense_layers-1)
		elif type(dropout_rate) == list and len(dropout_rate) == num_dense_layers-1:
			self.dropout_rate = dropout_rate
		else:
			raise ValueError("dropout_rate should be integer, float or a list of length num_dense_layers-1")

		if max_pooling is None:
			self.max_pooling = max_pooling
		elif len(max_pooling)!=num_conv_layers or not all([isinstance(i,list) for i in max_pooling]) or not all([len(i)==2 for i in max_pooling]):
			raise ValueError("max_pooling should be None or length num_conv_layers and contain lists of length 2")
		else:
			self.max_pooling = max_pooling

		self.padding = padding
		self.activation = activation
		self.vscope = vscope
		self.reuse=reuse

		with tf.variable_scope(self.vscope, reuse=self.reuse) as self.vs:
			self.train_flag_ph = tf.placeholder(tf.bool, name="train_flag_ph")
			self.on_off_ph = tf.placeholder(tf.bool, name="film_flag_ph")  # Allows custom parameters to be fed in as FiLM parameters
			self.params_ph = tf.placeholder(tf.float32, shape=[None, units[-1][1]], name="film_params_ph")

	def __call__(self, in_feats, ft_conv=None, ft_dense=None):
		with tf.variable_scope(self.vs, reuse=self.reuse, auxiliary_name_scope=False) as self.vs1:
			with tf.name_scope(self.vs1.original_name_scope):
				H = in_feats
				for l in range(self.num_conv_layers):
					if ft_conv is not None:  # finetuning on
						H = tf.layers.conv1d(H, self.filters[l][0], self.filters[l][1], padding=self.padding, activation=self.activation,
							kernel_initializer=tf.constant_initializer(ft_conv[l][0]), bias_initializer=tf.constant_initializer(ft_conv[l][1]))
					else:  # no finetuning
						H = tf.layers.conv1d(H, self.filters[l][0], self.filters[l][1], padding=self.padding, activation=self.activation)
					if self.max_pooling is not None:
						H = tf.layers.max_pooling1d(H, pool_size=self.max_pooling[l][0], strides=self.max_pooling[l][1])

				H = tf.reshape(H, [-1, self.units[0][0]])  # units[0][0] should be the number of filters in the last layer * spatial size after final pooling
				for l in range(self.num_dense_layers):				
					if l != self.num_dense_layers-1:  # don't do the below for the last layer.
						if ft_dense is not None:
							H = tf.layers.dense(H, self.units[l][1], activation=self.activation, kernel_initializer=tf.constant_initializer(ft_dense[l][0]),
        										bias_initializer=tf.constant_initializer(ft_dense[l][1]))
						else:
							H = tf.layers.dense(H, self.units[l][1], activation=self.activation)
						H = tf.layers.dropout(inputs=H, rate=self.dropout_rate[l], training=self.train_flag_ph)
					else:
						if ft_dense is not None:
							H = tf.layers.dense(H, self.units[l][1], activation=None, kernel_initializer=tf.constant_initializer(ft_dense[l][0]),
        										bias_initializer=tf.constant_initializer(ft_dense[l][1]), name="Final_Dense_Layer")
						else:
							H = tf.layers.dense(H, self.units[l][1], activation=None, name="Final_Dense_Layer")

				H = tf.cond(self.on_off_ph, lambda : H, lambda : self.params_ph, name="Input_FiLM")
				return tf.expand_dims(H, -1)
        
