
import os
import json
import math
import random
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.ticker as ticker
import matplotlib.patches as mpatches
import matplotlib.patheffects as path_effects

import csv
import pandas as pd


def load_samples_data():
	
	# Root files
	samples_folder = "Samples"
	data_files = os.listdir(samples_folder)
	# print(data_files)

	# Load all the JSON files data for the ground truth poses of each frames and store them in a list
	ground_truth_data = []	# Contains, for each sample, the ground truth information (frame name and anchor pose) for each of the frames
	for ground_truth_file in data_files:
		if ground_truth_file.count("coordinates") > 0:
			with open(samples_folder + "/" + ground_truth_file) as f:
				ground_truth_data.append(json.load(f))

	# Load all the JSON files data for the anchor images and store them in a list
	anchor_images_data = []	# Contains, for each sample, the main anchor image data (width and height of the anchor image)
	for anchor_images_file in data_files:
		if anchor_images_file.count("test_marker") > 0 and anchor_images_file.count("_data") > 0:
			with open(samples_folder + "/" + anchor_images_file) as f:
				anchor_images_data.append(json.load(f))

	# Load all the JSON files data in the sensor measurements
	sensor_data = []	# Contains, for each sample, the sensor data (acceleration and orientation) for each of the frames
	for sensor_file in data_files:
		if sensor_file.count("sensor") > 0:
			with open(samples_folder + "/" + sensor_file) as f:
				sensor_data.append(json.load(f))

	# Return the loaded data
	return ground_truth_data, anchor_images_data, sensor_data

def load_results_data():
	# Load the JSON file data for the detection results
	detection_results_file = "Results/detection_data.json"
	tracking_results_file = "Results/tracking_data.json"
	detection_results_data = None
	tracking_results_data = None
	if os.path.exists(detection_results_file):
		with open(detection_results_file) as f:
			detection_results_data = json.load(f)
	if os.path.exists(tracking_results_file):
		with open(tracking_results_file) as f:
			tracking_results_data = json.load(f)
	# Return the loaded data
	return detection_results_data, tracking_results_data

# Process the data to calculate the translation and rotation errors for the detection results
def process_detection_data(ground_truth_data, anchor_images_data, detection_results_data, print_debug = False):

	# Compute a JSON data with the ground truth results in the same format as the detection results data (for ech sample, for each frame, store the position and rotation from the ground truth coordinates data)
	ground_truth_results_data = []
	for i in range(len(ground_truth_data)):
		ground_truth_results_data.append([])
		size = [float(anchor_images_data[i]["width"]), float(anchor_images_data[i]["height"])]
		ground_truth_data_sample = ground_truth_data[i]
		ground_truth_data_sample = sorted(ground_truth_data_sample, key=lambda x: x["imageName"])
		for j in range(len(ground_truth_data_sample)):
			corners = ground_truth_data_sample[j]["controlPoints"] 
			corners = [[float(corner[0]), float(corner[1])] for corner in corners]
			pose = translate_corners_positions_to_pose(corners, size)
			position = pose["position"]
			rotation = pose["rotation"]
			ground_truth_results_data[i].append({
				"position": position, 
				"rotation": rotation
			})
	if print_debug: print(json.dumps(ground_truth_results_data, indent=2))

	# Save the ground truth results data to a JSON file
	with open("Results/ground_truth_final_poses.json", "w") as f:
		json.dump(ground_truth_results_data, f, indent=2)

	# Compute the errors for each detection result
	errors = {}
	for algorithm in detection_results_data:
		errors[algorithm] = []
		for i in range(len(detection_results_data[algorithm])):
			errors[algorithm].append([])
			if print_debug: print("Algorithm: ", algorithm, " Sample: ", i+1)
			if print_debug: print("> Length: ", len(detection_results_data[algorithm][i]))
			for j in range(len(detection_results_data[algorithm][i])):
				# Compute the translation and rotation errors for the current frame
				detected_position = None
				if detection_results_data[algorithm][i][j] is not None and "position" in detection_results_data[algorithm][i][j]:
					detected_position = detection_results_data[algorithm][i][j]["position"]
				detected_rotation = None
				if detection_results_data[algorithm][i][j] is not None and "rotation" in detection_results_data[algorithm][i][j]:
					detected_rotation = detection_results_data[algorithm][i][j]["rotation"]
				ground_truth_position = ground_truth_results_data[i][j]["position"]
				ground_truth_rotation = ground_truth_results_data[i][j]["rotation"]
				translation_error = None
				if detected_position != None and ground_truth_position != None:
					translation_error = math.sqrt((detected_position[0] - ground_truth_position[0])**2 + (detected_position[1] - ground_truth_position[1])**2 + (detected_position[2] - ground_truth_position[2])**2)
					translation_error = translation_error * 0.1	# Convert to units
				rotation_error = None
				if detected_rotation != None and ground_truth_rotation != None:
					# Find the angle between the two orientations (angle between the 2 vectors pointing to the direction of the angles)
					ground_truth_direction = [math.cos(math.radians(ground_truth_rotation[0])) * math.cos(math.radians(ground_truth_rotation[1])), math.sin(math.radians(ground_truth_rotation[0])) * math.cos(math.radians(ground_truth_rotation[1])), math.sin(math.radians(ground_truth_rotation[1]))]
					detected_direction = [math.cos(math.radians(detected_rotation[0])) * math.cos(math.radians(detected_rotation[1])), math.sin(math.radians(detected_rotation[0])) * math.cos(math.radians(detected_rotation[1])), math.sin(math.radians(detected_rotation[1]))]
					dot_product = ground_truth_direction[0] * detected_direction[0] + ground_truth_direction[1] * detected_direction[1] + ground_truth_direction[2] * detected_direction[2]
					rotation_error = math.degrees(math.acos(dot_product))
				# Store the errors
				errors[algorithm][i].append({
					"translation_error": translation_error,
					"rotation_error": rotation_error
				})
	if print_debug: print(json.dumps(errors, indent=2))

	# Print the average errors for each algorithm and each sample
	if print_debug: 
		for algorithm in errors:
			print("Algorithm: ", algorithm)
			for i in range(len(errors[algorithm])):
				translation_errors = [frame["translation_error"] for frame in errors[algorithm][i] if frame["translation_error"] != None]
				rotation_errors = [frame["rotation_error"] for frame in errors[algorithm][i] if frame["rotation_error"] != None]
				average_translation_error = np.mean(translation_errors) if len(translation_errors) > 0 else None
				average_rotation_error = np.mean(rotation_errors) if len(rotation_errors) > 0 else None
				print("Sample #" + str(i+1) + ": Translation Error: ", average_translation_error, " Rotation Error: ", average_rotation_error)
			print("-" * 50)

	# Save the errors data to a JSON file
	with open("Results/detection_final_errors.json", "w") as f:
		json.dump(errors, f, indent=2)

# Process the data to calculate the translation and rotation errors for the tracking results
def process_tracking_data(ground_truth_data, anchor_images_data, sensor_data, tracking_results_data, print_debug = False):
	
	# Compute a JSON data with the ground truth results in the same format as the tracking results data (for ech sample, for each frame, store the position and rotation from the ground truth coordinates data)
	ground_truth_results_data = []
	for i in range(len(ground_truth_data)):
		ground_truth_results_data.append([])
		size = [float(anchor_images_data[i]["width"]), float(anchor_images_data[i]["height"])]
		ground_truth_data_sample = ground_truth_data[i]
		ground_truth_data_sample = sorted(ground_truth_data_sample, key=lambda x: x["imageName"])
		for j in range(len(ground_truth_data_sample)):
			corners = ground_truth_data_sample[j]["controlPoints"] 
			corners = [[float(corner[0]), float(corner[1])] for corner in corners]
			pose = translate_corners_positions_to_pose(corners, size)
			position = pose["position"]
			rotation = pose["rotation"]
			ground_truth_results_data[i].append({
				"position": position, 
				"rotation": rotation
			})
	if print_debug: print(json.dumps(ground_truth_results_data, indent=2))

	# Save the ground truth results data to a JSON file
	with open("Results/ground_truth_final_poses.json", "w") as f:
		json.dump(ground_truth_results_data, f, indent=2)

	# Compute the errors for each tracking result
	errors = {}
	for algorithm in tracking_results_data:
		errors[algorithm] = []
		for i in range(len(tracking_results_data[algorithm])):
			errors[algorithm].append([])
			if print_debug: print("Algorithm: ", algorithm, " Sample: ", i+1)
			if print_debug: print("> Length: ", len(tracking_results_data[algorithm][i]))
			for j in range(len(tracking_results_data[algorithm][i])):
				# Compute the translation and rotation errors for the current frame
				detected_position = None
				if tracking_results_data[algorithm][i][j] is not None and "position" in tracking_results_data[algorithm][i][j]:
					detected_position = tracking_results_data[algorithm][i][j]["position"]
				detected_rotation = None
				if tracking_results_data[algorithm][i][j] is not None and "rotation" in tracking_results_data[algorithm][i][j]:
					detected_rotation = tracking_results_data[algorithm][i][j]["rotation"]
				ground_truth_position = ground_truth_results_data[i][j]["position"]
				ground_truth_rotation = ground_truth_results_data[i][j]["rotation"]
				translation_error = None
				if detected_position != None and ground_truth_position != None:
					translation_error = math.sqrt((detected_position[0] - ground_truth_position[0])**2 + (detected_position[1] - ground_truth_position[1])**2 + (detected_position[2] - ground_truth_position[2])**2)
					translation_error = translation_error * 0.1	# Convert to units
				rotation_error = None
				if detected_rotation != None and ground_truth_rotation != None:
					# Find the angle between the two orientations (angle between the 2 vectors pointing to the direction of the angles)
					ground_truth_direction = [math.cos(math.radians(ground_truth_rotation[0])) * math.cos(math.radians(ground_truth_rotation[1])), math.sin(math.radians(ground_truth_rotation[0])) * math.cos(math.radians(ground_truth_rotation[1])), math.sin(math.radians(ground_truth_rotation[1]))]
					detected_direction = [math.cos(math.radians(detected_rotation[0])) * math.cos(math.radians(detected_rotation[1])), math.sin(math.radians(detected_rotation[0])) * math.cos(math.radians(detected_rotation[1])), math.sin(math.radians(detected_rotation[1]))]
					dot_product = ground_truth_direction[0] * detected_direction[0] + ground_truth_direction[1] * detected_direction[1] + ground_truth_direction[2] * detected_direction[2]
					rotation_error = math.degrees(math.acos(dot_product))
				# Store the errors
				errors[algorithm][i].append({
					"translation_error": translation_error,
					"rotation_error": rotation_error
				})
	if print_debug: print(json.dumps(errors, indent=2))

	# Print the average errors for each algorithm and each sample
	if print_debug: 
		for algorithm in errors:
			print("Algorithm: ", algorithm)
			for i in range(len(errors[algorithm])):
				translation_errors = [frame["translation_error"] for frame in errors[algorithm][i] if frame["translation_error"] != None]
				rotation_errors = [frame["rotation_error"] for frame in errors[algorithm][i] if frame["rotation_error"] != None]
				average_translation_error = np.mean(translation_errors) if len(translation_errors) > 0 else None
				average_rotation_error = np.mean(rotation_errors) if len(rotation_errors) > 0 else None
				print("Sample #" + str(i+1) + ": Translation Error: ", average_translation_error, " Rotation Error: ", average_rotation_error)
			print("-" * 50)

	# Save the errors data to a JSON file
	with open("Results/tracking_final_errors.json", "w") as f:
		json.dump(errors, f, indent=2)

# Translate an array of 4 corners (top left, top right, bottom left, bottom right) to a pose given the width and height of the anchor image
def translate_corners_positions_to_pose(corners, size):
	if corners == None or len(corners) != 4:
		raise Exception("Corners array must contain exactly 4 elements.")

	# Camera resolution and field of view in degrees
	camera_resolution = [1080, 1920]
	camera_fov = 60.0

	# Convert corners coordinates from [0, 1080] and [0,1920] to [-1, 1]
	original_corners = corners
	for i in range(len(corners)):
		corners[i][0] = (float(corners[i][0]) / float(camera_resolution[0])) * 2.0 - 1.0
		corners[i][1] = (float(corners[i][1]) / float(camera_resolution[1])) * 2.0 - 1.0

	# Calculate the center of the image from the corners
	image_center_2d = [(corners[0][0] + corners[1][0] + corners[2][0] + corners[3][0]) / 4.0, (corners[0][1] + corners[1][1] + corners[2][1] + corners[3][1]) / 4.0]

	# Calculate width and height in world units based on the corners
	width_1 = math.sqrt((corners[0][0] - corners[1][0])**2 + (corners[0][1] - corners[1][1])**2)
	width_2 = math.sqrt((corners[2][0] - corners[3][0])**2 + (corners[2][1] - corners[3][1])**2)
	width = (width_1 + width_2) / 2.0
	height_1 = math.sqrt((corners[0][0] - corners[2][0])**2 + (corners[0][1] - corners[2][1])**2)
	height_2 = math.sqrt((corners[1][0] - corners[3][0])**2 + (corners[1][1] - corners[3][1])**2)
	height = (height_1 + height_2) / 2.0

	if width == 0 or height == 0:
		raise Exception("Invalid corners provided. The width and height of the image cannot be zero.\nCorners: " + str(corners) + "\nOriginal Corners: " + str(original_corners))

	# Calculate the scale factor (assume the provided size is in some world space units)
	scale_x = float(size[0]) / width if width != 0 else 0
	scale_y = float(size[1]) / height if height != 0 else 0

	# Adjust the image center to reflect scaling
	scaled_center = [image_center_2d[0] * scale_x, image_center_2d[1] * scale_y, 0]

	# Define the image's 3D position assuming it's facing the camera
	camera_fov_radians = math.radians(camera_fov)
	fixed_distance = (size[1] / 2.0) / math.tan(camera_fov_radians / 2.0)

	# The image center in 3D space
	image_center_3d = [scaled_center[0], scaled_center[1], fixed_distance]

	# Calculate the orientation of the image
	right = [corners[1][0] - corners[0][0], corners[1][1] - corners[0][1], 0]
	up = [corners[2][0] - corners[0][0], corners[2][1] - corners[0][1], 0]
	forward = [right[1] * up[2] - right[2] * up[1], right[2] * up[0] - right[0] * up[2], right[0] * up[1] - right[1] * up[0]]	

	# Normalize the vectors
	right_norm = math.sqrt(right[0]**2 + right[1]**2)
	up_norm = math.sqrt(up[0]**2 + up[1]**2)
	forward_norm = math.sqrt(forward[0]**2 + forward[1]**2 + forward[2]**2)
	right = [right[0] / right_norm, right[1] / right_norm]
	up = [up[0] / up_norm, up[1] / up_norm] if up_norm != 0 else [0, 0]
	forward = [forward[0] / forward_norm, forward[1] / forward_norm, forward[2] / forward_norm] if forward_norm != 0 else [0, 0, 0]

	# Calculate the orientation (in angles) of the image
	orientation = [math.degrees(math.atan2(forward[1], forward[0])), math.degrees(math.asin(forward[2])), math.degrees(math.atan2(up[0], up[1]))]

	# Return the pose containing the position and rotation
	return {"position": image_center_3d, "rotation": orientation}

# Main function
def compute_errors():

	# Load the samples data
	ground_truth_data, anchor_images_data, sensor_data = load_samples_data()
	# print(json.dumps(ground_truth_data, indent=2))
	# print(json.dumps(anchor_images_data, indent=2))
	# print(json.dumps(sensor_data, indent=2))

	# Load the results data
	detection_results_data, tracking_results_data = load_results_data()
	# print(json.dumps(detection_results_data, indent=2))
	# print(json.dumps(tracking_results_data, indent=2))

	# Process the data to calculate the translation and rotation errors for the detection results
	process_detection_data(ground_truth_data, anchor_images_data, detection_results_data)

	# Process the data to calculate the translation and rotation errors for the tracking results
	process_tracking_data(ground_truth_data, anchor_images_data, sensor_data, tracking_results_data)

# Function to plot the results using different plots
def plot_results(plot_detection=False, plot_tracking=False, plot_summary_graph=False, plot_optpimizations_fps = False):

	# Load the detection errors
	with open("Results/detection_final_errors.json") as f:
		detection_errors = json.load(f)
		
	# Load the tracking errors
	with open("Results/tracking_final_errors.json") as f:
		tracking_errors = json.load(f)

	# List of detection and tracking algorithms
	detection_algorithm_names = list(detection_errors.keys())
	tracking_algorithm_names = list(tracking_errors.keys())

	# List of detection samples to consider
	sample_numbers = [1,2,3,4,5]

	# Plots metadata
	grid_opacity = 0.35
	detection_colors = [
		# Detection
		"#999999",
		"#e41a1c",
		"#377eb8",
		"#4daf4a",
		"#984ea3",
		# Tracking
		"#ff7f00",
		"#f5d32c",
		"#a65628",
		"#f781bf",
		"#999999"
	]
	tracking_colors = detection_colors[::-1]

	# Save the legend of the various graphs to separate files
	save_legend_to_separate_file = False
	still_show_embedded_legend_on_some_graphs = True

	# Save 2 legend files with the colors of the algorithms for both detection and tracking
	if save_legend_to_separate_file:
		plt.close("all")
		plt.clf()
		plt.cla()
		plt.figure(figsize=(5, 3), dpi=200)
		legend_elements = []
		for i in range(len(detection_algorithm_names)):
			label = detection_algorithm_names[i]
			legend_elements.append(mpatches.Patch(color=detection_colors[i], label=label))
		plt.legend(handles=legend_elements, loc='center',title='Detection Algorithms', ncol=1, title_fontsize='large', fontsize='medium')
		plt.axis('off')
		plt.savefig("Results/Plots/detection_algorithms_legend.png", bbox_inches='tight')
		plt.close("all")
		plt.clf()
		plt.cla()
		plt.figure(figsize=(5, 3), dpi=200)
		legend_elements = []
		for i in range(len(tracking_algorithm_names)):
			label = tracking_algorithm_names[i]
			legend_elements.append(mpatches.Patch(color=tracking_colors[i], label=label))
		plt.legend(handles=legend_elements, loc='center',title='Tracking Algorithms', ncol=1, title_fontsize='large', fontsize='medium')
		plt.axis('off')
		plt.savefig("Results/Plots/tracking_algorithms_legend.png")

	# Auxiliary function to plot and/or save a scatterplot for the given algorithms and the given samples
	def plot_scatterplot(algorithms, samples, plot, save, filename):

		plt.close("all")
		plt.clf()
		plt.cla()
		plt.close()

		# Plot the translation and rotation errors in an X/Y scatteerplot, merging all the samples for each algorithm and color coding each algorithm with a different color
		marker_types = [
			# Circle
			"o",
			# Plus filled
			"P",
			# Square
			"s",
			# Diamond (big)
			"D",
			# Triangle
			"^",
			# # Plus
			# "+",
			# # Cross
			# "x",
			# # Star
			# "*",
		]
		x_label = "Translation Error (units)"
		y_label = "Rotation Error (degrees)"
		# Calculate the max values for the axes
		x_max = 0
		y_max = 0
		for algorithm in detection_errors:
			for sample in detection_errors[algorithm]:
				for frame in sample:
					translation_error = frame["translation_error"]
					rotation_error = frame["rotation_error"]
					if translation_error != None and translation_error > x_max:
						x_max = translation_error
					if rotation_error != None and rotation_error > y_max:
						y_max = rotation_error
		# Ceil the max values
		x_max = math.ceil(x_max * 10) / 10
		y_max = math.ceil(y_max)
		# Create a single scatterplot for all the algorithms, with different colors for each algorithm
		points_size = 50
		plt.figure(
			# Fullscreen
			figsize=(16, 9),
			dpi=80,
			# Title
			facecolor='w',
			edgecolor='k'
			
		)
		plt.xlim(0, x_max)
		plt.ylim(0, y_max)
		for i, algorithm in enumerate(detection_errors):
			x_values = []
			y_values = []
			opacity = 1.0
			low_opacity = 0.1
			if algorithm not in algorithms:
				opacity = low_opacity
			samples_to_consider = None
			samples_to_consider = detection_errors[algorithm]
			for j in range(len(samples_to_consider)):
				if j+1 not in samples:
					opacity = low_opacity
				elif algorithm in algorithms:
					opacity = 1.0
				sample = samples_to_consider[j]
				for frame in sample:
					translation_error = frame["translation_error"]
					rotation_error = frame["rotation_error"]
					if translation_error != None and rotation_error != None:
						# print("Translation error: ", translation_error)
						x_values.append(translation_error)
						y_values.append(rotation_error)
				label = algorithm + " #" + str(j+1)
				scatterplot = plt.scatter(x_values, y_values, color=detection_colors[i], label=label, s=points_size, marker=marker_types[j])
				scatterplot.set_alpha(opacity)
				# Clear the values for the next sample
				x_values = []
				y_values = []
		# Add labels and title
		plt.xlabel(x_label)
		plt.ylabel(y_label)
		append_to_title = ""
		if len(algorithms) == 1:
			append_to_title = " (highlighting algorithm " + algorithms[0] + ")"
		elif len(samples) == 1:
			append_to_title = " (highlighting sample #" + str(samples[0]) + ")"
		plt.title("Translation and Rotation Errors for Detection Algorithms" + append_to_title)
		plt.grid(alpha=grid_opacity)
		# Show a custom legend (for colors and shapes separatedly, with first all colors and algorithm names, then all shapes as black symbols encoding the sample numbers
		algorithm_names = list(detection_errors.keys())
		num_samples = len(detection_errors[algorithm_names[0]])
		legend_elements = []
		for i in range(len(algorithm_names)):
			label = algorithm_names[i]
			legend_elements.append(plt.Line2D([0], [0], marker="s", color=detection_colors[i], label=label, markersize=10, linestyle='None'))
		for i in range(num_samples):
			label = "Sample #" + str(i+1)
			legend_elements.append(plt.Line2D([0], [0], marker=marker_types[i], color='k', label=label, markersize=10, linestyle='None'))
		# Show the legend on the graph
		show_legend_on_graph = \
			not save_legend_to_separate_file \
			or (
				still_show_embedded_legend_on_some_graphs and \
				(len(algorithms) > 1 and len(samples) > 1)
			)
		if show_legend_on_graph:
			plt.legend(handles=legend_elements, loc='upper left',title='Algorithms and Samples', ncol=2, title_fontsize='large', fontsize='medium')
		# Save the plot to a file
		if save:
			plt.savefig("Results/Plots/" + filename + ".png", dpi=200, bbox_inches='tight')
		# Show the plot
		if plot:
			plt.show()
		# Save only the legend to a separate file if needed
		if save_legend_to_separate_file:
			plt.close("all")
			plt.clf()
			plt.cla()
			plt.figure(figsize=(5, 3), dpi=200)
			plt.legend(handles=legend_elements, loc='center',title='Algorithms and Samples', ncol=2, title_fontsize='large', fontsize='medium')
			plt.axis('off')
			plt.savefig("Results/Plots/detection_scatterplot_legend.png")

	# Plot the scatterplot for ALL algorithms and each sample
	for sample in sample_numbers:
		plot_scatterplot(detection_algorithm_names, [sample], plot=False, save=True, filename="detection_errors_all_sample_" + str(sample))

	# Plot the scatterplot for each algorithm and ALL samples
	for algorithm in detection_algorithm_names:
		plot_scatterplot([algorithm], sample_numbers, plot=False, save=True, filename="detection_errors_algorithm_" + algorithm + "_all_samples")

	# Plot the scatterplot for ALL  algorithms and ALL samples
	plot_scatterplot(detection_algorithm_names, sample_numbers, plot=plot_detection, save=True, filename="detection_errors_all_algorithms_all_samples")

	# Clear the plot
	plt.close("all")
	plt.clf()
	plt.cla()
	plt.close()

	# Plot a single figure with 2 graphs, containing one box plot for each algorithm (for position and rotation respectively) considering all possible data in all samples
	# NOTE: show the box plots vertically, with, on the X axis, the names of the algorithms, and on the Y axis, the values of the errors
	all_translation_errors = {}
	all_rotation_errors = {}
	# Aggregate all the translation and rotation errors for each algorithm
	for algorithm in tracking_errors:
		for sample in tracking_errors[algorithm]:
			for fps in sample:
				translation_error = fps["translation_error"]
				rotation_error = fps["rotation_error"]
				if translation_error != None:
					if algorithm not in all_translation_errors:
						all_translation_errors[algorithm] = []
					all_translation_errors[algorithm].append(translation_error)
				if rotation_error != None:
					if algorithm not in all_rotation_errors:
						all_rotation_errors[algorithm] = []
					all_rotation_errors[algorithm].append(rotation_error)

	def plot_boxplot(data, title, ylabel, filename):
		plt.figure(figsize=(11, 9), dpi=80)
		boxplot = plt.boxplot(
			data.values(), labels=data.keys(), 
			widths=0.5, 
			patch_artist=True, 
			boxprops=dict(linewidth=2),
			whiskerprops=dict(linewidth=2),
			capprops=dict(linewidth=2),
			medianprops=dict(linewidth=2)
		)
		# change meanline colors based on the algorithm (stored in "colors" list)
		for i, algorithm in enumerate(data.keys()):
			plt.setp(boxplot['medians'][i], color="white")
			plt.setp(boxplot['boxes'][i], color=tracking_colors[i])
			plt.setp(boxplot['whiskers'][i*2], color=tracking_colors[i])
			plt.setp(boxplot['whiskers'][i*2 + 1], color=tracking_colors[i])
			plt.setp(boxplot['caps'][i*2], color=tracking_colors[i])
			plt.setp(boxplot['caps'][i*2 + 1], color=tracking_colors[i])
		plt.title(title)
		plt.ylabel(ylabel)
		plt.grid(alpha=grid_opacity)
		# Show a legend with the colors for each algorithm
		# legend_elements = []
		# for i in range(len(data.keys())):
		# 	label = list(data.keys())[i]
		# 	legend_elements.append(mpatches.Patch(color=tracking_colors[i], label=label))
		# plt.legend(handles=legend_elements, loc='upper right',title='Algorithms', ncol=1, title_fontsize='large', fontsize='medium')
		# Save the plot to a file and/or show it
		plt.savefig("Results/Plots/" + filename + ".png", dpi=200, bbox_inches='tight')
		if plot_tracking:
			plt.show()

	# Create the box plots for the translation errors
	plot_boxplot(all_translation_errors, "Translation Errors for Tracking Algorithms", "Translation Error (units)", "tracking_translation_errors_all_algorithms_all_samples")

	# Create the box plots for the rotation errors
	plot_boxplot(all_rotation_errors, "Rotation Errors for Tracking Algorithms", "Rotation Error (degrees)", "tracking_rotation_errors_all_algorithms_all_samples")

	# Clear the plot
	plt.close("all")
	plt.clf()
	plt.cla()
	plt.close()

	# Create parallel coordinate plots for the tabular detection and tracking results data
	# Show number of undetected frames, average translation errors, average rotation errors, and execution times for each algorithm (encoded with the various colors) for detection algorithms, 
	# 	and then average translation errors, average rotation errors, and execution times for each algorithm (encoded with the various colors) for tracking algorithms
	
	# Load the CSV file with the tabular data
	detection_data = []
	with open("Results/Tables/detection_table_data.csv") as f:
		reader = csv.reader(f)
		detection_data = list(reader)
	detection_data = [row for row in detection_data if len(row) > 0]
	tracking_data = []
	with open("Results/Tables/tracking_table_data.csv") as f:
		reader = csv.reader(f)
		tracking_data = list(reader)
	tracking_data = [row for row in tracking_data if len(row) > 0]

	# NOTE: data is stored as:
	'''
	[
		["Algorithm", "Errors", "Translation Error", "Rotation Error", "Execution Time"],
		["Algorithm 1", "10", "0.5", "0.2", "0.1"],
		["Algorithm 2", "5", "0.3", "0.1", "0.2"],
		...
	]
	'''

	# Auxiliary function to plot a parallel coordinates plot for the given data
	def plot_parallel_coordinates(data,  title=None, filename=None, colors=None):
		"""
		Plots a parallel coordinates visualization from string data, with the first row as headers.

		Parameters:
			data (list of lists): Input data with all values as strings. 
								The first row contains headers, and subsequent rows contain data values.
			title (str): Title for the plot (optional).
			filename (str): If provided, saves the plot to this filename (optional).

		Returns:
			None. Displays or saves the plot.
		"""
		# Extract headers and numeric data
		headers = data[0][1:]  # Skip the first column (e.g., "Algorithm")
		raw_data = data[1:]    # Skip the header row

		# Reverse the data to have the algorithms in the same order as the colors
		raw_data = raw_data[::-1]
		colors = colors[: len(raw_data)]
		colors = colors[::-1]
		
		# Extract algorithm names and numeric values
		algorithm_names = [row[0] for row in raw_data]
		numeric_data = [[float(value) for value in row[1:]] for row in raw_data]

		# Determine the number of dimensions
		dims = len(headers)
		x = range(dims)
		fig, axes = plt.subplots(1, dims - 1, figsize=(dims * 2, 6), sharey=False)

		# Calculate the limits on the data for normalization
		min_max_range = []
		for dimension_values in zip(*numeric_data):
			mn = min(dimension_values)
			mx = max(dimension_values)
			if mn == mx:
				mn -= 0.5
				mx += 0.5
			r = float(mx - mn)
			pad_range = r * 0.1
			# min_max_range.append((mn, mx, r))
			min_max_range.append((mn - pad_range, mx + pad_range, r))

		# Normalize the data sets
		norm_data_sets = []
		for ds in numeric_data:
			norm_data_sets.append([
				(value - min_max_range[dimension][0]) / min_max_range[dimension][2]
				for dimension, value in enumerate(ds)
			])

		# Plot the datasets on all subplots
		for i, ax in enumerate(axes):
			for dsi, data in enumerate(norm_data_sets):
				color = colors[dsi] if colors else 'b'
				ax.plot([x[i], x[i + 1]], [data[i], data[i + 1]], color, label=algorithm_names[dsi], linewidth=3)
			ax.set_xlim([x[i], x[i + 1]])

		# Set the x-axis ticks and labels
		# NOTE: Show the axis names directly above the Y-axis
		x_offset = 0  # Define your x offset here
		x_offset_last = 1  # Define your x offset here
		y_offset = -0.125   # Define your y offset here
		for dimension, (ax, label) in enumerate(zip(axes, headers[:-1])):
			ax.xaxis.set_major_locator(ticker.FixedLocator([x[dimension]]))
			ticks = len(ax.get_yticks())
			mn, mx, r = min_max_range[dimension]
			labels = [f'{mn + i * (r / (ticks - 1)):.2f}' for i in range(ticks)]
			ax.set_yticklabels(labels)
			final_x_offset = x_offset 
			ax.set_title(label, loc='center', x=final_x_offset, y=y_offset)  # Adjust the pad and loc value as needed
			ax.xaxis.set_major_formatter(plt.NullFormatter())

		# Adjust the last axis ticks
		# NOTE: Show the last axis label to the right of the Y-axis
		last_ax = plt.twinx(axes[-1])
		mn, mx, r = min_max_range[-1]
		ticks = len(last_ax.get_yticks())
		labels = [f'{mn + i * (r / (ticks - 1)):.2f}' for i in range(ticks)]
		last_ax.set_yticklabels(labels)
		last_ax.set_title(headers[-1], loc='center', x=x_offset_last, y=y_offset)  # Adjust the pad and loc value as needed

		# Add the overall title
		if title:
			title_y_offset = 0.96
			plt.suptitle(title, y=title_y_offset, fontsize=16)

		# Remove the borders
		for ax in axes:
			ax.spines['top'].set_visible(False)
			ax.spines['bottom'].set_visible(False)
		last_ax.spines['top'].set_visible(False)
		last_ax.spines['bottom'].set_visible(False)

		# Add a legend
		axes[0].legend(loc='upper left', bbox_to_anchor=(1.05, 1), title="Algorithms")

		# Show text with the names of each line above each line
		for i, ax in enumerate(axes):
			for dsi, data in enumerate(norm_data_sets):
				if i != 0:
					continue
				color = colors[dsi] if colors else 'b'
				pos_offset = [0.02, 0.02]
				if len(axes) < 3:
					pos_offset[0] = 0.01
				vertical_alignment = 'bottom'
				# if algorithm_names[dsi] == "SensorTracking":
				# 	vertical_alignment = 'top'
				# 	pos_offset[1] = -0.0525
				txt = ax.text(x[i] + pos_offset[0], data[i] + pos_offset[1], algorithm_names[dsi], color=color, fontsize=10, ha='left', va=vertical_alignment, weight='bold')
				txt.set_path_effects([path_effects.Stroke(linewidth=2, foreground='white'), path_effects.Normal()])

		# Adjust subplot spacing
		plt.subplots_adjust(wspace=0)

		# Set the size of the figure
		fig.set_size_inches(17, 3.5)
		
		# Save the plot to a file
		plt.savefig("Results/Plots/" + filename + ".png", dpi=200, bbox_inches='tight')
		# Show the plot
		if plot_summary_graph:
			plt.show()
	
	# Create the parallel coordinates plot for the detection data
	axes_titles = ["Errors", "Translation Error", "Rotation Error", "Recognition Execution Time", "Detection Execution Time"]
	# data = pd.DataFrame(detection_data[1:], columns=detection_data[0])
	data = detection_data
	print(data)
	plot_parallel_coordinates(data, "Detection Results", "detection_parallel_coordinates", detection_colors)

	# Create the parallel coordinates plot for the tracking data
	axes_titles = ["Translation Error", "Rotation Error", "Execution Time"]
	# data = pd.DataFrame(tracking_data[1:], columns=tracking_data[0])
	data = tracking_data
	print(data)
	plot_parallel_coordinates(data, "Tracking Results", "tracking_parallel_coordinates", tracking_colors)

	# Clear the plot
	plt.close("all")
	plt.clf()
	plt.cla()
	plt.close()

	# Plot the FPS for the optimizations in a histogram showing the frequency for each FPS range (e.g., 0-10, 10-20, 20-30, etc.)
	# Load the optimization FPS data
	with open("Results/optimization_fps.json") as f:
		optimization_fps = json.load(f)

	# Get the FPS ranges
	min_fps = 0
	max_fps = 0
	for algorithm in optimization_fps:
		for sample in optimization_fps[algorithm]:
			for fps in sample:
				if fps != None and fps < min_fps:
					min_fps = math.floor(fps)
				if fps != None and fps > max_fps:
					max_fps = math.ceil(fps)
	# Define the ranges
	buckets = -1
	range_size = 5
	# If no buckets are defined, calculate the number of buckets based on the range size
	if buckets == -1:
		buckets = int((max_fps - min_fps) / range_size)
	else:
		range_size = int((max_fps - min_fps) / buckets)
	# Create the ranges
	fps_ranges = []
	for i in range(buckets):
		fps_ranges.append((
			(min_fps + i * (max_fps - min_fps) / buckets),
			(min_fps + (i+1) * (max_fps - min_fps) / buckets)
		))
	# Count the number of frames in each range for each algorithm
	fps_data = {}
	for algorithm in optimization_fps:
		fps_data[algorithm] = [0] * buckets
		for sample in optimization_fps[algorithm]:
			for fps in sample:
				if fps != None:
					for i in range(buckets):
						if fps >= fps_ranges[i][0] and fps < fps_ranges[i][1]:
							fps_data[algorithm][i] += 1
	# Plot the histogram
	plt.figure(figsize=(12, 8), dpi=80)
	x = np.arange(buckets)
	min_width = 1.0
	width = min_width / len(fps_data)
	for i, algorithm in enumerate(fps_data):
		plt.bar(x + i * width, fps_data[algorithm], width, label=algorithm, color=detection_colors[i], edgecolor='white', linewidth=1.5)
	plt.xlabel('FPS Range', labelpad=15)
	plt.ylabel('Frequency', labelpad=10)
	plt.title('Optimization FPS Histogram')
	# plt.xticks(x + width * (len(fps_data) - 1) / 2, [str(int(fps_ranges[i][0])) + "-" + str(int(fps_ranges[i][1])) for i in range(buckets)])
	# plt.xticks(x - width / 2.0, [str(int(fps_ranges[i][0])) + "-" + str(int(fps_ranges[i][1])) for i in range(buckets)])
	plt.xticks(x - width / 2.0, ["" for i in range(buckets)])
	# Show all ticks
	max_y = int(max([max(fps_data[algorithm]) for algorithm in fps_data]))
	y_tick_range = 25
	plt.yticks(range(0, max_y + y_tick_range, y_tick_range))
	if not save_legend_to_separate_file or still_show_embedded_legend_on_some_graphs:
		plt.legend()
	plt.grid(alpha=grid_opacity, axis='y')
	# Show the x axis labels
	for i in range(buckets):
		# range_text = str(int(fps_ranges[i][0])) + "-" + str(int(fps_ranges[i][1]))
		range_text = str(i * range_size) + "-" + str((i+1) * range_size - 1)
		plt.text(i + width * (len(fps_data) - 1) / 2, -10, range_text, rotation=0, fontsize=8, ha='center', va='center')
	plt.savefig("Results/Plots/optimization_fps_histogram.png", dpi=200, bbox_inches='tight')
	if plot_optpimizations_fps:
		plt.show()

# Get tabular data results (average translation and rotation errors for each algorithm and each sample in both detection and tracking) and store them in a JSON file
def get_tabular_data():

	# Load the detection errors
	with open("Results/detection_final_errors.json") as f:
		detection_errors = json.load(f)
	# Load the tracking errors
	with open("Results/tracking_final_errors.json") as f:
		tracking_errors = json.load(f)

	# Load the detection execution times
	with open("Results/detection_execution_times.json") as f:
		detection_execution_times = json.load(f)
	# Load the tracking execution times
	with open("Results/tracking_execution_times.json") as f:
		tracking_execution_times = json.load(f)

	# Load the optimization FPS data
	with open("Results/optimization_fps.json") as f:
		optimization_fps = json.load(f)

	# List of algorithms and samples to consider
	algorithm_names = list(detection_errors.keys())
	sample_numbers = [1,2,3,4,5]

	# Compute the average translation and rotation errors for each algorithm and each sample for the detection results
	detection_tabular_data = {}
	for algorithm in detection_errors:
		detection_tabular_data[algorithm] = {}
		for i in range(len(detection_errors[algorithm])):
			translation_errors = [frame["translation_error"] for frame in detection_errors[algorithm][i] if frame["translation_error"] != None]
			rotation_errors = [frame["rotation_error"] for frame in detection_errors[algorithm][i] if frame["rotation_error"] != None]
			average_translation_error = np.mean(translation_errors) if len(translation_errors) > 0 else None
			average_rotation_error = np.mean(rotation_errors) if len(rotation_errors) > 0 else None
			detection_tabular_data[algorithm]["Sample #" + str(i+1)] = {
				"Translation Error": average_translation_error,
				"Rotation Error": average_rotation_error
			}

	# Compute the average translation and rotation errors for each algorithm and each sample for the tracking results
	tracking_tabular_data = {}
	for algorithm in tracking_errors:
		tracking_tabular_data[algorithm] = {}
		for i in range(len(tracking_errors[algorithm])):
			translation_errors = [frame["translation_error"] for frame in tracking_errors[algorithm][i] if frame["translation_error"] != None]
			rotation_errors = [frame["rotation_error"] for frame in tracking_errors[algorithm][i] if frame["rotation_error"] != None]
			average_translation_error = np.mean(translation_errors) if len(translation_errors) > 0 else None
			average_rotation_error = np.mean(rotation_errors) if len(rotation_errors) > 0 else None
			tracking_tabular_data[algorithm]["Sample #" + str(i+1)] = {
				"Translation Error": average_translation_error,
				"Rotation Error": average_rotation_error
			}

	# Add average for all samples in each algorithm
	for algorithm in detection_tabular_data:
		translation_errors = [detection_tabular_data[algorithm]["Sample #" + str(i+1)]["Translation Error"] for i in range(len(detection_tabular_data[algorithm])) if detection_tabular_data[algorithm]["Sample #" + str(i+1)]["Translation Error"] != None]
		rotation_errors = [detection_tabular_data[algorithm]["Sample #" + str(i+1)]["Rotation Error"] for i in range(len(detection_tabular_data[algorithm])) if detection_tabular_data[algorithm]["Sample #" + str(i+1)]["Rotation Error"] != None]
		average_translation_error = np.mean(translation_errors) if len(translation_errors) > 0 else None
		average_rotation_error = np.mean(rotation_errors) if len(rotation_errors) > 0 else None
		detection_tabular_data[algorithm]["Average"] = {
			"Translation Error": average_translation_error,
			"Rotation Error": average_rotation_error
	}
	for algorithm in tracking_tabular_data:
		translation_errors = [tracking_tabular_data[algorithm]["Sample #" + str(i+1)]["Translation Error"] for i in range(len(tracking_tabular_data[algorithm])) if tracking_tabular_data[algorithm]["Sample #" + str(i+1)]["Translation Error"] != None]
		rotation_errors = [tracking_tabular_data[algorithm]["Sample #" + str(i+1)]["Rotation Error"] for i in range(len(tracking_tabular_data[algorithm])) if tracking_tabular_data[algorithm]["Sample #" + str(i+1)]["Rotation Error"] != None]
		average_translation_error = np.mean(translation_errors) if len(translation_errors) > 0 else None
		average_rotation_error = np.mean(rotation_errors) if len(rotation_errors) > 0 else None
		tracking_tabular_data[algorithm]["Average"] = {
			"Translation Error": average_translation_error,
			"Rotation Error": average_rotation_error
		}

	# Add number of detected frames (non null frames)
	for algorithm in detection_tabular_data:
		for sample in detection_tabular_data[algorithm]:
			if sample != "Average":
				continue
			detected_frames = 0
			total_frames = 0
			for i in range(len(detection_errors[algorithm])):
				for frame_infos in detection_errors[algorithm][i]:
					if frame_infos["translation_error"] != None:
						detected_frames += 1
					total_frames += 1
			detection_tabular_data[algorithm][sample]["Undetected Frames"] = total_frames - detected_frames

	# Save the tabular data to a JSON file
	with open("Results/tabular_data.json", "w") as f:
		json.dump({
			"detection": detection_tabular_data,
			"tracking": tracking_tabular_data
		}, f, indent=2)

	# Print a separator
	print("\n" + "#" * 80)

	# Print the average for all algoritihms for each algorithm for both detection and tracking
	print("\nDetection Results (Average):")
	for algorithm in detection_tabular_data:
		print("  " + algorithm)
		for sample in detection_tabular_data[algorithm]:
			if sample != "Average":
				continue
			# print("Sample: ", sample)
			print("    Undetected Frames: ", detection_tabular_data[algorithm][sample]["Undetected Frames"])
			print("    Translation Error: ", round(detection_tabular_data[algorithm][sample]["Translation Error"], 3))
			print("    Rotation Error: ", round(detection_tabular_data[algorithm][sample]["Rotation Error"], 1))
	print("\n" + "=" * 50)
	print("\nTracking Results (Average):")
	for algorithm in tracking_tabular_data:
		print("  " + algorithm)
		for sample in tracking_tabular_data[algorithm]:
			if sample != "Average":
				continue
			# print("Sample: ", sample)
			print("    Translation Error: ", round(tracking_tabular_data[algorithm][sample]["Translation Error"], 3))
			print("    Rotation Error: ", round(tracking_tabular_data[algorithm][sample]["Rotation Error"], 1))

	# Compute the average execution times of each algorithms for both detection and tracking
	detection_execution_times_average = {}
	for algorithm in detection_execution_times:
		image_recognition_execution_times = []
		pose_detection_execution_times = []
		for sample in detection_execution_times[algorithm]:
			for frame_infos in sample:
				image_recognition_time = frame_infos[0]
				pose_detection_time = frame_infos[1]
				image_recognition_execution_times.append(image_recognition_time)
				pose_detection_execution_times.append(pose_detection_time)
		average_execution_time = [
			np.mean(image_recognition_execution_times) if len(image_recognition_execution_times) > 0 else None,
			np.mean(pose_detection_execution_times) if len(pose_detection_execution_times) > 0 else None
		]
		detection_execution_times_average[algorithm] = average_execution_time
	tracking_execution_times_average = {}
	for algorithm in tracking_execution_times:
		execution_times = []
		for sample in tracking_execution_times[algorithm]:
			for frame_infos in sample:
				execution_times.append(frame_infos)
		average_execution_time = np.mean(execution_times) if len(execution_times) > 0 else None
		tracking_execution_times_average[algorithm] = average_execution_time

	# Save the execution times data to a JSON file
	with open("Results/execution_times.json", "w") as f:
		json.dump({
			"detection": detection_execution_times_average,
			"tracking": tracking_execution_times_average
		}, f, indent=2)

	# Print a separator
	print("\n" + "#" * 80)

	# Print the average execution times for each algorithm for both detection and tracking
	print("\nDetection Execution Times (Average):")
	for algorithm in detection_execution_times_average:
		for i in range(2):
			print("  " + algorithm + " (" + ("recognition" if i == 0 else "detection")  + "): ", round(detection_execution_times_average[algorithm][i], 3))
	print("\n" + "=" * 50)
	print("\nTracking Execution Times (Average):")
	for algorithm in tracking_execution_times_average:
		print("  " + algorithm + ": ", round(tracking_execution_times_average[algorithm], 3))

	# Print a separator
	print("\n" + "#" * 80)

	# Compute the final data to show in the various tables
	# NOTE: tables are shown separatedly for detection and tracking algorithms, and for each sample
	detection_table_columns = ["Algorithm", "Errors", "Translation Error", "Rotation Error", "Recognition Execution Time", "Pose Detection Execution Time"]
	tracking_table_columns = ["Algorithm", "Translation Error", "Rotation Error", "Execution Time"]
	detection_table_data = []
	tracking_table_data = []
	for algorithm in detection_tabular_data:
		for sample in detection_tabular_data[algorithm]:
			if sample != "Average":
				continue
			detection_table_data.append([
				algorithm,
				detection_tabular_data[algorithm][sample]["Undetected Frames"],
				round(detection_tabular_data[algorithm][sample]["Translation Error"], 3),
				round(detection_tabular_data[algorithm][sample]["Rotation Error"], 1),
				round(detection_execution_times_average[algorithm][0], 3),
				round(detection_execution_times_average[algorithm][1], 3)
			])
	for algorithm in tracking_tabular_data:
		for sample in tracking_tabular_data[algorithm]:
			if sample != "Average":
				continue
			tracking_table_data.append([
				algorithm,
				round(tracking_tabular_data[algorithm][sample]["Translation Error"], 3),
				round(tracking_tabular_data[algorithm][sample]["Rotation Error"], 1),
				round(tracking_execution_times_average[algorithm], 3)
			])
			
	# Save the tabular data to a CSV file
	with open("Results/Tables/detection_table_data.csv", "w") as f:
		writer = csv.writer(f)
		writer.writerow(detection_table_columns)
		writer.writerows(detection_table_data)
	with open("Results/Tables/tracking_table_data.csv", "w") as f:
		writer = csv.writer(f)
		writer.writerow(tracking_table_columns)
		writer.writerows(tracking_table_data)

	# Print a separator
	print("\n" + "#" * 80)

	# Prints a table from the tabular data (use no external libraries)
	def print_table(columns, data):
		# Calculate the maximum length for each column
		columns_lengths = [len(column) for column in columns]
		for row in data:
			for i, value in enumerate(row):
				if len(str(value)) > columns_lengths[i]:
					columns_lengths[i] = len(str(value))
		# Print the table header
		for i, column in enumerate(columns):
			print(column.ljust(columns_lengths[i]), end=" | ")
		print()
		# Print the separator
		for i in range(len(columns)):
			print("-" * columns_lengths[i], end=" | ")
		print()
		# Print the table data
		for row in data:
			for i, value in enumerate(row):
				print(str(value).ljust(columns_lengths[i]), end=" | ")
			print()

	# Print the detection table
	print("\nDetection Table:")
	print_table(detection_table_columns, detection_table_data)
	print("\n" + "=" * 50)
	# Print the tracking table
	print("\nTracking Table:")
	print_table(tracking_table_columns, tracking_table_data)

	# Print a separator
	print("\n" + "#" * 80)

	# Print data for the optimizations FPS (compute average FPS for each optimization)
	# NOTE: data for optimization FPSs are in format:
	'''
	{
		"Optimization 1": [		# FPS for each frame of each sample for the first optimization method
			[10, 20, 30, ...],	# FPS for each frame for the first sample
			[15, 25, 35, ...],	# FPS for each frame for the second sample
			...
		],
		...
	}
	'''
	optimization_algorithms = list(optimization_fps.keys())
	optimization_fps_average = {}
	for algorithm in optimization_fps:
		fps_values = []
		for sample in optimization_fps[algorithm]:
			for frame_infos in sample:
				fps_values.append(frame_infos)
		average_fps = np.mean(fps_values) if len(fps_values) > 0 else None
		optimization_fps_average[algorithm] = average_fps

	# Save the optimization FPS data to a JSON file
	with open("Results/optimization_fps_average.json", "w") as f:
		json.dump(optimization_fps_average, f, indent=2)

	# Print the average FPS for each optimization
	print("\nOptimization FPS (Average):")
	for algorithm in optimization_fps_average:
		print("  " + algorithm + ": ", round(optimization_fps_average[algorithm], 3))

	# Print a separator
	print("\n" + "#" * 80)

# Execute the main functions
# compute_errors()
# get_tabular_data()
plot_results(False, False, False, False)