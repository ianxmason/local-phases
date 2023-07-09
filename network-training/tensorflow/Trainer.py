import tensorflow as tf
import numpy as np
import pickle
from collections import defaultdict

class Trainer(object):
	def __init__(self, model, inputs, outputs, iterators, reinitialize, config, tefo_iterator=None):
		"""
		Currently Trainer assumes iterators are initialised from string handles

		model: function representing a neural network model
		loss: function
		reinitialize: list of booleans, which iterators should be reinitialized at the end of an epoch
		"""
		self.model = model
		self.inputs = inputs
		self.outputs = outputs
		self.iterators = iterators
		self.reinitialize = reinitialize
		self.tefo_iterator = tefo_iterator	
		self.config = config	

	def train_styles(self, loss, learning_rate, batch_size, epochs, styles_list, save_path, mname, sess, itrtr_feed_dict, feed_dict, handle_keys, save_scopes):
		pred = self.model(*self.inputs)
		error = loss(*self.outputs, pred)  # loss should be a function
		# tf.summary.scalar('Loss', error)

		"""Optimisation"""
		params = tf.trainable_variables()
		gradients = tf.gradients(error, params)
		optimizer = tf.train.AdamOptimizer(learning_rate)
		update_step = optimizer.apply_gradients(zip(gradients, params))

		"""Monitoring"""
		# merged = tf.summary.merge_all()
		summary_writer = tf.summary.FileWriter(save_path + mname + "Summary")
		
		sess.run(tf.global_variables_initializer())
			
		handles = []
		for itrtr_list in self.iterators:
			handles.append([sess.run(itrtr.string_handle()) for itrtr in itrtr_list])

		print("Initializing datasets")
		for itrtr_list in self.iterators:
			for itrtr in itrtr_list:
				sess.run(itrtr.initializer, feed_dict=itrtr_feed_dict) 
		print("Initialized")

		per_style_epoch = defaultdict(int)

		cur_style = 0
		for epoch in range(epochs):
			batch_losses = defaultdict(list)			
			while True:
				i = cur_style % len(styles_list)
				try:
					for j in range(len(handle_keys)):
						feed_dict[handle_keys[j]] = handles[j][i]
					# _, loss_monitor, summary = sess.run([update_step, error, merged], feed_dict=feed_dict)
					_, loss_monitor = sess.run([update_step, error], feed_dict=feed_dict)
					batch_losses[styles_list[i]].append(loss_monitor)
					# cur_style += 1 
				except tf.errors.OutOfRangeError:  # This means we have come to the end of one of the datasets
					print("\r {} dataset completed".format(styles_list[i]))
					per_style_epoch[styles_list[i]] += 1					
					# reinitialize whichever dataset has ended
					for idx in range(len(self.iterators)):
						if self.reinitialize[idx]:
							sess.run(self.iterators[idx][i].initializer, feed_dict=itrtr_feed_dict) 
				finally:
					cur_style += 1
				if any(x == epoch+1 for x in per_style_epoch.values()):  # one epoch is one pass through the smallest dataset
					break

			all_losses = []
			for l in batch_losses.values():
				all_losses += l
			epoch_loss = np.mean(all_losses)  # close enough
			epoch_summary = tf.Summary(value=[tf.Summary.Value(tag='_Total Loss', simple_value=epoch_loss)])
			summary_writer.add_summary(epoch_summary, global_step=epoch)
			for style in styles_list:
				style_summary = tf.Summary(value=[tf.Summary.Value(tag='{} Loss'.format(style), simple_value=np.mean(batch_losses[style]))])
				summary_writer.add_summary(style_summary, global_step=epoch)
			print("epoch {} completed.  Loss {}\n".format(epoch, epoch_loss))

			if epoch==0 or (epoch+1)%30==0:  # 1 indexed epochs
			# if epoch==0 or (epoch+1)%50==0:  # for finetuning trains faster

				inputs_dict = {k.name: k for k in itrtr_feed_dict.keys()}
				for k in feed_dict.keys():
					inputs_dict[k.name] = k

				outputs_dict = {"prediction": pred}

				print("Saving...\n")
				# Save whole model structure for socket
				tf.saved_model.simple_save(  
					sess, save_path + mname + str(epoch+1), inputs_dict, outputs_dict
				)

				# Save specified variables for learning from new data
				if save_scopes is not None:
					for fname, scp in save_scopes.items():
						print("##")
						print(fname)
						print(scp)
						print("##")
						saver = tf.train.Saver(tf.get_collection(tf.GraphKeys.GLOBAL_VARIABLES, scp), max_to_keep=1)
						saver.save(sess, save_path + mname + fname + str(epoch+1) + ".ckpt")

				print("Saved.\n")


	def resave_model(self, loss, learning_rate, batch_size, epochs, styles_list, save_path, mname, sess, itrtr_feed_dict, feed_dict, handle_keys, save_scopes):
		"""
		Saves a model with e.g. new data paths, without doing any more training.
		"""
		pred = self.model(*self.inputs)
		error = loss(*self.outputs, pred)  # loss should be a function
		# tf.summary.scalar('Loss', error)

		"""Optimisation"""
		params = tf.trainable_variables()
		gradients = tf.gradients(error, params)
		optimizer = tf.train.AdamOptimizer(learning_rate)
		update_step = optimizer.apply_gradients(zip(gradients, params))

		"""Monitoring"""
		# merged = tf.summary.merge_all()
		summary_writer = tf.summary.FileWriter(save_path + mname + "Summary")
		
		sess.run(tf.global_variables_initializer())
			
		handles = []
		for itrtr_list in self.iterators:
			handles.append([sess.run(itrtr.string_handle()) for itrtr in itrtr_list])

		print("Initializing datasets")
		for itrtr_list in self.iterators:
			for itrtr in itrtr_list:
				sess.run(itrtr.initializer, feed_dict=itrtr_feed_dict) 
		print("Initialized")

		per_style_epoch = defaultdict(int)


		inputs_dict = {k.name: k for k in itrtr_feed_dict.keys()}
		for k in feed_dict.keys():
			inputs_dict[k.name] = k

		outputs_dict = {"prediction": pred}

		print("Saving...\n")
		# Save whole model structure for socket
		tf.saved_model.simple_save(  
			sess, save_path + mname + "Generalisation", inputs_dict, outputs_dict
		)

		# Save specified variables for learning from new data
		if save_scopes is not None:
			for fname, scp in save_scopes.items():
				saver = tf.train.Saver(tf.get_collection(tf.GraphKeys.GLOBAL_VARIABLES, scp), max_to_keep=1)
				saver.save(sess, save_path + mname + fname + "Generalisation" + ".ckpt")

		print("Saved.\n")
