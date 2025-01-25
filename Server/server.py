# General libraries
import os
import random
import json
import traceback
# Flask libraries
from flask import Flask, request, jsonify
# Email libraries
import smtplib
from email.mime.text import MIMEText

# Flask server initializazion
app = Flask(__name__)

# CORS headers
@app.after_request
def after_request(response):
	response.headers.add('Access-Control-Allow-Origin', '*')
	response.headers.add('Access-Control-Allow-Headers', 'Content-Type,Authorization')
	response.headers.add('Access-Control-Allow-Methods', 'GET,POST')
	return response

"""
# Route to perform predictions
@app.route('/predict', methods=['GET', 'POST'])
def predict():
	try:
		test = False
		if test:
			# Wait for some seconds then return a random weight
			import time
			time.sleep(1)
			return jsonify({"weight": 14})
		else:
			# Get the data (data is sent through a GET request as a JSON object request with a certain JSON body, containing the sensor data)
			data = request.get_json()
			# Extract the measurements from the data
			measurements = data["sensor_data"]
			# print("Measurements (" + str(len(measurements)) + "):")
			# print(measurements)
			# Data will contain a list of flattened measurements
			input_data = []
			for i in range(len(measurements)):
				data_point = ModelData.format_measurements(measurements[i], MEASUREMENTS, NORMALIZE_SENSOR_DATA, NORMALIZATION_RANGE)
				input_data.extend(data_point)
			input_data = torch.tensor(input_data, dtype=torch.float64).to(device)
			# Perform the prediction
			weight,model_output = model.predict(input_data)
			# Convert the weight and model output from a tensor to a float
			weight = weight.item()
			model_output = model_output.item()
			print("Predicted weight: " + str(weight))
			print("Model output: " + str(model_output))
			# Return the prediction
			return jsonify({"weight": weight, "model_output": model_output})
	except Exception as e:
		print("An error occurred:\n" + str(e))
		return jsonify({"error": str(e)})
"""

# Route to login
@app.route('/login', methods=['POST'])
def login():
	try:
		# Get the data (data is sent through a POST request as a JSON object request with a certain JSON body, containing the user email)
		data = request.get_json()
		# Get the "email" field (only the email field is sent to then generate a one time password sent via email to the user)
		email = data["email"]
		# Check that the email is in the correct format
		if "@" not in email or "." not in email:
			return jsonify({"success": False, "error": "Invalid email format"})
		# Generate a one time password of 5 digits
		num_digits = 5
		otp = random.randint(10**(num_digits-1), 10**num_digits-1)
		# Print the OTP
		print("OTP: " + str(otp))
		# Store the OTP in the "otp.json" file (containing a JSON objects with the emails as keys and the OTP as values), adding the new OTP to the list
		otp_file_path = "./mysite/otp.json"
		otp_file = open(otp_file_path, "r")
		otp_data = json.load(otp_file)
		otp_file.close()
		# Modify the OTP data (check if the email key is already present in the otp data, if it is, modify the OTP, if it is not, add the email and OTP)
		# otp_data.append({"email": email, "otp": str(otp)})
		otp_data[email] = str(otp)
		# Save the OTP data to the file
		otp_file = open(otp_file_path, "w")
		json.dump(otp_data, otp_file)
		otp_file.close()
		# Send the email with the OTP
		send_email(email, otp)
		# Return the response
		return jsonify({"success": True})
	except Exception as e:
		print("An error occurred:\n" + str(e))
		return jsonify({"success": False, "error": str(e)})
def send_email(email, otp):
	# Functoin to try to send an email on behalf of a certain gmail account (accessed ia the password)
	def try_send_email(my_email, my_password, displayed_email=None):
		# creates SMTP session
		s = smtplib.SMTP('smtp.gmail.com', 587)
		# s = smtplib.SMTP('smtp.gmail.com', 465)
		# start TLS for security
		s.starttls()
		# Authentication
		print("Trying to login with email: " + my_email + " and password: " + my_password)
		s.login(my_email, my_password)
		# Sender email
		sender_email = displayed_email if displayed_email is not None else my_email
		# message to be sent
		message_subject = "ShARED App - One Time Password"
		message_body = "Your one time password for ShARED is:\n\n" + str(otp)
		message = MIMEText(message_body)
		message['Subject'] = message_subject
		message['From'] = sender_email
		message['To'] = email
		# sending the mail
		s.sendmail(sender_email, email, message.as_string())
		# terminating the session
		s.quit()
	# Try to send the email with the OTP using the email and password
	my_email = "login.sharedapp@gmail.com"
	my_password = "ncbv fbyy wgit ohqj"	# app password
	try:
		# My own email and password
		# NOTE: because Google stopped support for new sign-ins from mail clients using username and password, an app password is used instead of the main password to send the email
		try_send_email(my_email, my_password)
	except Exception as e:
		print("\nAn error occurred while sending the email with the OTP using the main email and password:\n" + str(e))
		print("\nTrying to send the email with the OTP using the alternative email and password...")
		# Alternative email and password
		alternative_email = "foodpal.otp@gmail.com"
		alternative_password = "kmid sbfs thhs bjus"	# app password
		try_send_email(alternative_email, alternative_password, my_email)

# Route to verify the OTP
@app.route('/verify', methods=['POST'])
def verify():
	try:
		# Get the data (data is sent through a POST request as a JSON object request with a certain JSON body, containing the relevant data)
		data = request.get_json()
		# Get the "email" and "otp" fields (both the email and the OTP are sent to then verify the OTP)
		email = data["email"]
		otp = data["otp"]
		# Verify the OTP
		otp_file_path = "./mysite/otp.json"
		users_file_path = "./mysite/users.json"
		otp_file = open(otp_file_path, "r")
		otp_data = json.load(otp_file)
		otp_file.close()
		verified = False
		user_infos = []
		if email in otp_data:
			if otp_data[email] == str(otp):
				verified = True
				# Remove the OTP from the file
				del otp_data[email]
				otp_file = open(otp_file_path, "w")
				json.dump(otp_data, otp_file)
				otp_file.close()
				# Add the user in the users file (if it is not already present)
				users_file = open(users_file_path, "r")
				users_data = json.load(users_file)
				users_file.close()
				# Generate a username from the email
				username = email.split("@")[0]
				if username not in users_data:
					# Check if a user with the same username but different email is already present (in this case, append a number to the username)
					# 	NOTE: first user will always be "username", second user will be "username1", third user will be "username2", etc.
					username_base = username
					username_number = 1
					while username in users_data:
						if users_data[username]["email"] == email:
							break
						username = username_base + str(username_number)
						username_number += 1
					# Add the user to the users data
					users_data[username] = {"email": email, "username": username, "infos": []}
					users_file = open(users_file_path, "w")
					json.dump(users_data, users_file)
					users_file.close()
				# Get the user infos
				user_infos = users_data[username]["infos"]
		# Return the response
		if not verified:
			return jsonify({"verified": False, "error": "Invalid OTP"})
		else:
			# Get the user token
			user_token = get_user_token(email)
			return jsonify({"verified": True, "username": username, "token": user_token, "infos": user_infos})
	except Exception as e:
		print("An error occurred:\n" + str(e))
		return jsonify({"verified": False, "error": str(e)})

# Function to get a user token from a user email
def get_user_token(email):
	# Generate a random deterministic user token starting from the email
	# NOTE: this is a simple deterministic hash function, a more complex hash function should be used for security in a real application
	#		A more secure way would be to randomly compute a user token at first and store it on the server for future checks
	max_token_length = 8
	user_token = 0
	for i in range(len(email)):
		user_token += ord(email[i]) * (i + 1) * 10 ** (i % max_token_length)
	user_token = user_token % (10 ** max_token_length)
	user_token_str = str(user_token).zfill(max_token_length)
	return user_token_str

# Route to save the content of the given JSON file as a new AR experience data
@app.route('/upload_experience', methods=['POST'])
def upload_experience():
	try:

		# Get the data (data is sent through a GET request as a JSON object request with a certain JSON body, containing the sensor data)
		data = request.get_json()
		# Get the "email" field (only the email field is sent to then generate a one time password sent via email to the user)
		email = data["email"]
		user_token = data["user_token"]
		experience = data["experience"]
		username = email.split("@")[0]

		# Get the existing experiences data
		experiences_file_path = "./mysite/experiences.json"
		experiences_file = open(experiences_file_path, "r")
		experiences_data = json.load(experiences_file)
		experiences_file.close()

		# Get the experience code and upload/update the experience
		experience_code = experience["code"]
		if experience_code == "":
			# Generate a new code until a unique one is found
			# NOTE: a random code generation is used for now since a few experiences exist and randomly generating a code is faster and leads to a unique code often,
			#		but a more efficient way to generate a unique code should be used when the number of experiences increases
			max_attempts = 1000
			completed = False
			for i in range(max_attempts):
				# Generate a random experience code as a 4 characters alphanumeric string
				experience_code = "#" + "".join(random.choices("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", k=4))
				if experience_code not in experiences_data:
					completed = True
					break
			if not completed:
				return jsonify({"uploaded": False, "error": "Could not generate a unique experience code, try again..."})
		else:
			# Check if the user is an experience with the given code exists, and if it exists, check if the user owns it
			if experience_code not in experiences_data:
				# Experience does not exist, return an error
				return jsonify({"uploaded": False, "error": "Unknown experience code"})
			# Check if the user is the owner of the experience
			if experiences_data[experience_code]["owner_email"] != email or user_token != experiences_data[experience_code]["user_token"]:
				# User is not the owner of the experience, return an error
				return jsonify({"uploaded": False, "error": "User is not the owner of the given experience"})
			# Update the experience code (the experience exists and the user is the owner of the experience)
			experience["code"] = experience_code
			
		# Add the experience to the experiences data
		experiences_data[experience_code] = experience
		# Add the owner email and token to the experience data (stored only on the server, never shared with the client)
		experiences_data[experience_code]["owner_email"] = email
		experiences_data[experience_code]["user_token"] = user_token

		# Save the experiences data
		experiences_file = open(experiences_file_path, "w")
		json.dump(experiences_data, experiences_file)
		experiences_file.close()

		# Return the response
		return jsonify({"uploaded": True, "code": experience_code})
	
	except Exception as e:
		print("An error occurred:\n" + str(e))
		return jsonify({"uploaded": False, "error": str(e)})
	
# Route to get the content of the given JSON file as a new AR experience data
@app.route('/get_experience', methods=['GET'])
def get_experience():
	try:
		# Get the data (data is sent through a GET request as a simple REST request with a "code" URL parameter)
		experience_code = request.args.get("code")
		if experience_code is None:
			return jsonify({"found": False, "error": "No experience code provided"})
		if experience_code[0] != "#":
			experience_code = "#" + experience_code

		# Load the experiences data
		experiences_file_path = "./mysite/experiences.json"
		experiences_file = open(experiences_file_path, "r")
		experiences_data = json.load(experiences_file)
		experiences_file.close()
		# Check if the experience code is in the experiences
		if experience_code not in experiences_data:
			return jsonify({"found": False, "error": "Unknown experience code"})
		
		# Get the experience (without the stored user email)
		experience = experiences_data[experience_code]
		# Delete the owner email from the experience data (only used internally by the server)
		del experience["owner_email"]
		del experience["user_token"]

		# Return the response
		return jsonify({"found": True, "result": experience})
	
	except Exception as e:
		print("An error occurred:\n" + str(e) + "\n" + traceback.format_exc())
		# return the error and the error line
		return jsonify({"found": False, "error": str(e)})


"""
# Route to return results about saved users in the server (as a JSONified string)
@app.route('/user_infos', methods=['GET', 'POST'])
def get_user_infos():
	try:
		# Get the data (data is sent through a GET request as a JSON object request with a certain JSON body, containing the sensor data)
		data = request.get_json()
		# Get the "email" field (only the email field is sent to then generate a one time password sent via email to the user)
		username = data["username"]
		# Load the users data
		users_file_path = "./mysite/users.json"
		users_file = open(users_file_path, "r")
		users_data = json.load(users_file)
		users_file.close()
		# Check if the URL parameters contain a field "email"
		authenticated = False
		if "email" in data:
			auth_email = data["email"]
			auth_username = auth_email.split("@")[0]
			if auth_username in users_data:
				if users_data[auth_username]["email"] == auth_email:
					authenticated = True
		# Check if the username is in the users
		if username not in users_data:
			return jsonify({"found": False, "error": "Unknown username"})
		# Get the user infos
		username = users_data[username]["username"]
		user_infos = users_data[username]["infos"]
		followed = [] if not authenticated else users_data[username]["followed"]
		# Return the response
		return jsonify({"found": True, "result": {"username": username, "infos": user_infos, "followed": followed}})
	except Exception as e:
		print("An error occurred:\n" + str(e))
		return jsonify({"found": False, "error": str(e)})
"""

"""
# Route to save the user infos
@app.route('/save_user_infos', methods=['POST'])
def save_infos():
	try:
		# Get the data (data is sent through a GET request as a JSON object request with a certain JSON body, containing the sensor data)
		data = request.get_json()
		# Get the "email" field (only the email field is sent to then generate a one time password sent via email to the user)
		email = data["email"]
		infos = data["infos"]
		username = email.split("@")[0]
		# Load the users data
		users_file_path = "./mysite/users.json"
		users_file = open(users_file_path, "r")
		users_data = json.load(users_file)
		users_file.close()
		# Check if the username is in the users
		if username not in users_data:
			return jsonify({"saved": False, "error": "Unknown user"})
		# Find the real username
		for user in users_data:
			if users_data[user]["email"] == email:
				username = user
				break
		# Get the current user infos (list of json objects, hence dictionaries)
		current_infos = users_data[username]["infos"]
		# Add the new infos to the list
		current_infos.append(infos)
		# Save the user infos
		users_data[username]["infos"] = current_infos
		# Save the users data
		users_file = open(users_file_path, "w")
		json.dump(users_data, users_file)
		users_file.close()
		# Return the response
		return jsonify({"saved": True, "infos": current_infos})
	except Exception as e:
		print("An error occurred:\n" + str(e))
		return jsonify({"saved": False, "error": str(e)})
"""

"""
# Route to update the followed users
@app.route('/update_followed', methods=['POST'])
def update_followed():
	try:
		# Get the data (data is sent through a GET request as a JSON object request with a certain JSON body, containing the sensor data)
		data = request.get_json()
		# Get the "email" field (only the email field is sent to then generate a one time password sent via email to the user)
		email = data["email"]
		followed = data["followed"]
		username = email.split("@")[0]
		# Load the users data
		users_file_path = "./mysite/users.json"
		users_file = open(users_file_path, "r")
		users_data = json.load(users_file)
		users_file.close()
		# Check if the username is in the users
		if username not in users_data:
			return jsonify({"updated": False, "error": "Unknown user"})
		# Find the real username
		for user in users_data:
			if users_data[user]["email"] == email:
				username = user
				break
		# Get the current followed users
		current_followed = users_data[username]["followed"]
		# Update the followed users
		current_followed = followed
		# Save the followed users
		users_data[username]["followed"] = current_followed
		# Save the users data
		users_file = open(users_file_path, "w")
		json.dump(users_data, users_file)
		users_file.close()
		# Return the response
		return jsonify({"updated": True})
	except Exception as e:
		print("An error occurred:\n" + str(e))
		return jsonify({"updated": False, "error": str(e)})
"""
