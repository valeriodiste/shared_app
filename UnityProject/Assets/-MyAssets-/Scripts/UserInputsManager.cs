using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class UserInputsManager : MonoBehaviour {


	// Track every user press and hold on mobile screen, the coordinates of the touch, ecc... (use the new Input system)

	private bool holding = false;
	private Vector2 touchPosition;
	private Vector2 touchUp;
	private Vector2 touchDown;
	private int touchPhase; // 0 if began, 1 if moved, 2 if ended, -1 if canceled/not touching

	private void Update() {
		// Check if the user is pressing on the screen (AR environment)
		if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) {
			// Get the touch
			TouchControl touch = Touchscreen.current.primaryTouch;
			// Set the touch position
			touchPosition = touch.position.ReadValue();
			if (!holding) {
				touchDown = touchPosition;
				touchPhase = 0;
			} else {
				touchPhase = 1;
			}
			holding = true;
		} else {
			// Reset the touch infos
			touchPosition = Vector2.zero;
			touchDown = Vector2.zero;
			if (holding) {
				touchUp = touchPosition;
				touchPhase = 2;
			} else {
				touchPhase = -1;
			}
			holding = false;
		}

	}

	/*
	private void Update() {
		// Check if the user is pressing on the screen (AR environment)
		if (Input.touchCount > 0) {
			// Get the touch
			Touch touch = Input.GetTouch(0);
			touchPhase = touch.phase;
			Debug.Log("Touch phase: " + touchPhase + ", position: " + touch.position);
			// Check if the user is pressing on the screen
			if (touch.phase == TouchPhase.Began) {
				// Set the touch position
				touchPosition = touch.position;
				touchDown = touchPosition;
				holding = true;
			}
			// Check if the user is moving the finger on the screen
			if (touch.phase == TouchPhase.Moved) {
				// Set the touch delta
				touchPosition = touch.position;
			}
			// Check if the user is releasing the finger from the screen
			if (touch.phase == TouchPhase.Ended) {
				// Set the touch up position
				touchUp = touch.position;
				holding = false;
			}
		} else {
			// Reset the touch infos
			touchPosition = Vector2.zero;
			touchUp = Vector2.zero;
			touchDown = Vector2.zero;
			touchPhase = TouchPhase.Canceled;
			holding = false;
		}
	}
	*/

	/// <summary>
	/// Returns true if the user is currently touching or holding the screen, and the current touch position, the touch position at the time the user started holding, and the touch position at the time the user released the screen.
	/// </summary>
	public bool GetTouchInfos(out Vector2 touchPosition, out Vector2 touchUp, out Vector2 touchDown, out int touchPhase) {
		touchPosition = this.touchPosition;
		touchUp = this.touchUp;
		touchDown = this.touchDown;
		touchPhase = this.touchPhase;
		return holding;
	}

}
