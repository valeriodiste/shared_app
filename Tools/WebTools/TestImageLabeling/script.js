
// Get the canvas
var canvas = document.getElementById('canvas');
var ctx = canvas.getContext('2d');
var canvasContainer = document.getElementById('canvas-container');

// List of images given as input by the user
let imageNames = [];
let images = [];
let active_image = null;
var active_image_index = 0;
var canvasScale = 1;

// Control points of the image (ordered as [TL, TR, BR, BL])
var control_points = [];
var control_points_coordinates = [];
var active_control_point_index = -1;
var controlPointsSize = 15;
let controlPointsClicks = 0;	// used for the "click to place" feature

document.addEventListener('DOMContentLoaded', async function () {

	// Resize the canvas
	resizeCanvas(1080, 1920);
	let resizeOnWindowResize = false;	// NOTE: will lose canvas content on resize if true
	if (resizeOnWindowResize || active_image == null) {
		window.addEventListener('resize', function () {
			resizeCanvas();
		});
	}

	// On file input change, load the images
	let fileInput = document.getElementById('image-input');
	fileInput.addEventListener('change', async function () {
		// Delete the stored control points
		localStorage.clear();
		// Show the loading screen
		let loadingScreen = document.getElementById('loading');
		loadingScreen.classList.remove('hidden');
		// Load the images
		images = [];
		let files = fileInput.files;
		for (let i = 0; i < files.length; i++) {
			let file = files[i];
			let reader = new FileReader();
			reader.onload = function (e) {
				let image = e.target.result;
				images.push(image);
				imageNames.push(file.name);
			};
			reader.readAsDataURL(file);
		}
		// Wait for all images to load
		await new Promise(function (resolve) {
			let interval = setInterval(function () {
				if (images.length == files.length) {
					clearInterval(interval);
					resolve();
				}
			}, 100);
		});
		let sortedIndices = Array.from(Array(images.length).keys());
		sortedIndices.sort(function (a, b) {
			return imageNames[a].localeCompare(imageNames[b]);
		});
		images = sortedIndices.map(function (i) { return images[i]; });
		imageNames = sortedIndices.map(function (i) { return imageNames[i]; });
		console.log(images);
		console.log(imageNames);
		// Hide the loading screen
		loadingScreen.classList.add('hidden');
		// Set the canvas image to the first image
		active_image_index = 0;
		setCanvasImage(images[active_image_index]);
	});

	// On click onto the JSON input, load the control points from the JSON file
	let jsonInput = document.getElementById('json-input');
	jsonInput.addEventListener('change', function () {
		let file = jsonInput.files[0];
		let reader = new FileReader();
		reader.onload = function (e) {
			let controlPoints = JSON.parse(e.target.result);
			console.log('Control points:\n', JSON.stringify(controlPoints, null, 2));
			// Save all the control points to the local storage
			console.log(imageNames);
			controlPoints.sort(function (a, b) {
				return imageNames.indexOf(a.imageName) - imageNames.indexOf(b.imageName);
			});
			console.log("New control points order:\n", JSON.stringify(controlPoints, null, 2));
			for (let i = 0; i < controlPoints.length; i++) {
				let imageName = controlPoints[i].imageName;
				let controlPointsObject = controlPoints[i];
				let controlPointsString = JSON.stringify(controlPointsObject);
				localStorage.setItem(imageName, controlPointsString);
			}
			initializeControlPoints(true);
		};
		reader.readAsText(file);
	});

	// Get the control points (ordered as [TL, TR, BR, BL])
	let controlPointsContainer = document.getElementById('control-points');
	let controlPointElements = controlPointsContainer.children;
	for (let i = 0; i < controlPointElements.length; i++) {
		let controlPoint = controlPointElements[i];
		control_points.push(controlPoint);
	}

	// On click (and hold) onto a canvas control point, set the active control point
	for (let i = 0; i < control_points.length; i++) {
		control_points[i].addEventListener('mousedown', function (e) {
			active_control_point_index = i;
			console.log('Active control point:', active_control_point_index);
		});
	}

	// On click onto the canvas, check if we should place the control pointns
	canvasContainer.addEventListener('click', function (e) {
		if (active_control_point_index >= control_points.length) return;
		let clickToPlaceCheckbox = document.getElementById('click-to-place');
		// Check if we should place the control points on click
		if (clickToPlaceCheckbox.checked) {
			let x = e.clientX - canvasContainer.offsetLeft - controlPointsSize / 2;
			let y = e.clientY - canvasContainer.offsetTop - controlPointsSize / 2;
			let index = control_points.length - 1 - controlPointsClicks;
			control_points_coordinates[index] = [x, y];
			refreshControlPoints();
			controlPointsClicks++;
		}
	});

	// On drag onto the canvas, move the active control point
	controlPointsContainer.addEventListener('mousemove', function (e) {
		if (active_control_point_index >= 0) {
			let x = e.clientX - canvasContainer.offsetLeft;
			let y = e.clientY - canvasContainer.offsetTop;
			control_points[active_control_point_index].style.left = x + 'px';
			control_points[active_control_point_index].style.top = y + 'px';
			control_points_coordinates[active_control_point_index] = [x, y];
			refreshControlPoints();
		}
	});

	// On release of the mouse button, unset the active control point
	document.addEventListener('mouseup', function (e) {
		active_control_point_index = -1;
		// Check if we should automatically save the control points
		let autoSave = document.getElementById('autosave').checked;
		if (autoSave) saveControlPoints();
	});

	// On click on next/previous image buttons, change the active image
	let nextButton = document.getElementById('next-image');
	nextButton.addEventListener('click', function () {
		if (active_image_index < images.length - 1) {
			active_image_index++;
			active_image_index = Math.min(images.length - 1, active_image_index);
			setCanvasImage(images[active_image_index]);
		}
		controlPointsClicks = 0;
	});
	let prevButton = document.getElementById('prev-image');
	prevButton.addEventListener('click', function () {
		if (active_image_index > 0) {
			active_image_index--;
			active_image_index = Math.max(0, active_image_index);
			setCanvasImage(images[active_image_index]);
		}
		controlPointsClicks = 0;
	});

	// On click onto the "reset" button, reset the control points to the corners of the canvas
	let resetButton = document.getElementById('reset-button');
	resetButton.addEventListener('click', function () {
		initializeControlPoints(false);
	});

	// On click onto the "save" button, save the control points for the current image name and control points infos to the local storage
	let saveButton = document.getElementById('save-button');
	saveButton.addEventListener('click', function () {
		// Save the coordinates of the control points
		saveControlPoints();
	});

	// On click onto the "download" button, download the control points for the current image name and control points infos as a JSON file
	let downloadButton = document.getElementById('download-button');
	downloadButton.addEventListener('click', function () {
		downloadSingleControlPoints();
	});

	// On click onto the "download all" button, download all the control points for all images as a JSON file
	let downloadAllButton = document.getElementById('download-all-button');
	downloadAllButton.addEventListener('click', function () {
		downloadAllControlPoints();
	});

	// On click on left and right arrow keys simulate the next and previous image buttons
	document.addEventListener('keydown', function (e) {
		if (e.key == 'ArrowRight') {
			nextButton.click();
		} else if (e.key == 'ArrowLeft') {
			prevButton.click();
		}
	});

});

// Resize the canvas (NOTE: will lose any canvas content on resize)
function resizeCanvas(width, height) {
	// Get the current active image if no reference width or height is given
	if (!width || !height) {
		if (active_image) {
			width = active_image.width;
			height = active_image.height;
		} else {
			width = 1080;
			height = 1920;
		}
	}
	// Resize the canvas to fit the image
	canvas.width = width;
	canvas.height = height;
	// Set the scale of the canvas element (tranform scale property) to make the bcanvas fully visible
	let max_width = canvasContainer.clientWidth;
	let max_height = canvasContainer.clientHeight;
	canvasScale = Math.min(max_width / width, max_height / height) * 0.975;
	canvas.style.transform = 'scale(' + canvasScale + ')';
}

// Set the canvas image
function setCanvasImage(image) {
	// Set the image to the canvas (wait for the image to load)
	let img = new Image();
	img.src = image;
	img.onload = function () {
		// Resize the canvas
		resizeCanvas(img.width, img.height);
		// Draw the image on the canvas
		ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
		// Set the active image
		active_image = img;
		// Initialize the control points
		initializeControlPoints();
	};
}

// Initialize the control points of the canvas to be on the corners of the canvas (also take into account the current canvas scale)
function initializeControlPoints(restoreSaved = true) {
	// Auxiliary function to set the control points to the corners of the canvas
	function setToImageCorners() {
		let width = canvas.width;
		let height = canvas.height;
		let scale = canvasScale;
		let offsetX = (canvasContainer.clientWidth - width * scale) / 2;
		let offsetY = (canvasContainer.clientHeight - height * scale) / 2;
		let pointSize = 15;
		let minX = offsetX - pointSize / 2;
		let minY = offsetY + height * scale - pointSize / 2;
		let maxY = offsetY - pointSize / 2;
		let maxX = offsetX + width * scale - pointSize / 2;
		control_points_coordinates = [[minX, minY], [maxX, minY], [maxX, maxY], [minX, maxY]];
	}
	// Check if the control points should be restored
	if (!restoreSaved) {
		// Set the control points to the corners of the canvas
		setToImageCorners();
	} else {
		// Restore the saved control points
		let savedImageInfos = localStorage.getItem(imageNames[active_image_index]);
		if (savedImageInfos) {
			let savedImageInfosObject = JSON.parse(savedImageInfos);
			console.log('Restoring saved control points:');
			console.log(savedImageInfosObject);
			let savedControlPoints = savedImageInfosObject.controlPoints;
			control_points_coordinates = savedControlPoints.map(function (point) {
				return getGlobalImageCoordinates(point[0], point[1]);
			});
		} else {
			// Set the control points to the corners of the canvas
			setToImageCorners();
		}
	}
	refreshControlPoints();
}

function getLocalImagePixelCoordinates(globalX, globalY) {
	let scale = canvasScale;
	let offsetX = (canvasContainer.clientWidth - canvas.width * scale) / 2;
	let offsetY = (canvasContainer.clientHeight - canvas.height * scale) / 2;
	let x = (globalX - offsetX) / scale + (controlPointsSize / scale) / 2;
	let y = (globalY - offsetY) / scale + (controlPointsSize / scale) / 2;
	y = canvas.height - y;
	return [x.toFixed(0), y.toFixed(0)];
}

function getGlobalImageCoordinates(localX, localY) {
	let scale = canvasScale;
	let offsetX = (canvasContainer.clientWidth - canvas.width * scale) / 2;
	let offsetY = (canvasContainer.clientHeight - canvas.height * scale) / 2;
	let additionalOffset = 10 * scale;
	let x = localX * scale + offsetX - controlPointsSize * scale * 2 + additionalOffset;
	let y = (canvas.height - localY) * scale + offsetY - controlPointsSize * scale * 2 + additionalOffset;
	return [x, y];
}

function refreshControlPoints() {
	// Check if control points are not being shown
	// Refresh the control points
	for (let i = 0; i < control_points.length; i++) {
		control_points[i].style.left = control_points_coordinates[i][0] + 'px';
		control_points[i].style.top = control_points_coordinates[i][1] + 'px';
		control_points[i].style.display = 'block';
	}
	// Set the control points infos
	let imageInfos = document.getElementById('image-info');
	imageInfos.innerHTML = '';
	let imageNumber = document.createElement('p');
	imageNumber.innerHTML = 'Image ' + (active_image_index + 1) + ' / ' + images.length;
	imageInfos.appendChild(imageNumber);
	let controlPointsTitle = document.createElement('h3');
	controlPointsTitle.innerHTML = 'Control points:'
	imageInfos.appendChild(controlPointsTitle);
	// let pointsNames = ['TL', 'TR', 'BR', 'BL'];
	let pointsNames = ["BL", "BR", "TR", "TL"];
	let childrenToAppend = [];
	for (let i = 0; i < control_points.length; i++) {
		let p = document.createElement('p');
		let xText = getLocalImagePixelCoordinates(control_points_coordinates[i][0], control_points_coordinates[i][1])[0];
		let yText = getLocalImagePixelCoordinates(control_points_coordinates[i][0], control_points_coordinates[i][1])[1];
		p.innerHTML = pointsNames[i] + ': (' + xText + ', ' + yText + ')';
		childrenToAppend.push(p);
	}
	for (let i = 0; i < childrenToAppend.length; i++) {
		imageInfos.appendChild(childrenToAppend[childrenToAppend.length - 1 - i]);
	}

}

// Function to save the coordinates of the control points to the local storage
function saveControlPoints() {
	// Save the coordinates of the control points
	let controlPointsCoordinates = control_points_coordinates.map(function (point) {
		return getLocalImagePixelCoordinates(point[0], point[1]);
	});
	// get a json object with the control points
	let controlPointsObject = {
		controlPoints: controlPointsCoordinates
	};
	// Save the control points to the local storage
	let controlPointsString = JSON.stringify(controlPointsObject);
	localStorage.setItem(imageNames[active_image_index], controlPointsString);
}

// Funciton to download the saved control points as a JSON file
function downloadSingleControlPoints() {
	// Get the control points from the local storage
	let controlPoints = localStorage.getItem(imageNames[active_image_index]);
	if (controlPoints) {
		// Create a blob with the control points
		let blob = new Blob([controlPoints], { type: 'application/json' });
		// Create a URL for the blob
		let url = URL.createObjectURL(blob);
		// Create a link to download the blob
		let a = document.createElement('a');
		a.href = url;
		a.download = imageNames[active_image_index] + '.json';
		a.click();
	}
}

// Function to download all the saved control points as a JSON file
function downloadAllControlPoints() {
	// Get the control points from the local storage for ALL images
	let controlPoints = [];
	for (let i = 0; i < imageNames.length; i++) {
		let controlPointsString = localStorage.getItem(imageNames[i]);
		if (controlPointsString) {
			let controlPointsObject = JSON.parse(controlPointsString);
			let object_to_add = {
				imageName: imageNames[i],
				controlPoints: controlPointsObject.controlPoints
			};
			controlPoints.push(object_to_add);
		}
	}
	// Create a blob with the control points
	let controlPointsBlob = new Blob([JSON.stringify(controlPoints)], { type: 'application/json' });
	// Create a URL for the blob
	let controlPointsUrl = URL.createObjectURL(controlPointsBlob);
	// Create a link to download the blob
	let a = document.createElement('a');
	a.href = controlPointsUrl;
	a.download = 'control_points.json';
	a.click();
}
