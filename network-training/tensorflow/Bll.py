import tensorflow as tf
import numpy as np

def bll(real_input, fake_input, num_bones, parents, mu, sig, offset):
	"""
	Bone length loss
	- remove trajectory
	- denormalise first
	"""	
	real_input = (real_input * sig) + mu
	fake_input = (fake_input * sig) + mu
	real_input = real_input[:, offset:]
	fake_input = fake_input[:, offset:]
	loss = tf.zeros_like(fake_input[:,0])
	for i, p in enumerate(parents):
		if p == -1: continue
		# Without velocities
		# loss += tf.abs(tf.sqrt(tf.square(fake_input[:, 9*p] - fake_input[:, 9*i]) +
		# tf.square(fake_input[:, 9*p + 1] - fake_input[:, 9*i + 1]) +
		# tf.square(fake_input[:, 9*p + 2] - fake_input[:, 9*i + 2])) - 
		# tf.sqrt(tf.square(real_input[:, 9*p] - real_input[:, 9*i]) +
		# tf.square(real_input[:, 9*p + 1] - real_input[:, 9*i + 1]) +
		# tf.square(real_input[:, 9*p + 2] - real_input[:, 9*i + 2])))

		# With velocities
		loss += tf.abs(tf.sqrt(tf.square(fake_input[:, 12*p] - fake_input[:, 12*i]) +
		tf.square(fake_input[:, 12*p + 1] - fake_input[:, 12*i + 1]) +
		tf.square(fake_input[:, 12*p + 2] - fake_input[:, 12*i + 2])) - 
		tf.sqrt(tf.square(real_input[:, 12*p] - real_input[:, 12*i]) +
		tf.square(real_input[:, 12*p + 1] - real_input[:, 12*i + 1]) +
		tf.square(real_input[:, 12*p + 2] - real_input[:, 12*i + 2])))

	loss = tf.reduce_mean(loss)/num_bones

	return loss
