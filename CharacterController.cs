using Godot;
using NakamacSharpTutorial;
using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;

/// <summary>
/// Handles local player movement and basic player node initialization.
/// Movement is applied only when this node is the multiplayer authority.
/// </summary>
public partial class CharacterController : CharacterBody2D
{
	/// <summary>
	/// Horizontal movement speed.
	/// </summary>
	public const float Speed = 300.0f;

	/// <summary>
	/// Upward impulse used for jump.
	/// </summary>
	public const float JumpVelocity = -400.0f;

	public PlayerInfo Info;

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	public float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

	public override void _Ready()
	{
		base._Ready();
	}

	/// <summary>
	/// Sets the player's visible name and initial spawn position.
	/// </summary>
	/// <param name="name">Player display identifier.</param>
	/// <param name="position">Spawn world position.</param>
	public void SetupPlayer(string name, Vector2 position){
		GlobalPosition = position;
		GetNode<Label>("Label").Text = name;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsMultiplayerAuthority()){
			Vector2 velocity = Velocity;

			// Add the gravity.
			if (!IsOnFloor())
				velocity.Y += gravity * (float)delta;

			// Handle Jump.
			if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
				velocity.Y = JumpVelocity;

			// Get the input direction and handle the movement/deceleration.
			// As good practice, you should replace UI actions with custom gameplay actions.
			Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
			if (direction != Vector2.Zero)
			{
				velocity.X = direction.X * Speed;
			}
			else
			{
				velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			}

			Velocity = velocity;
			MoveAndSlide();

		}
	}
}
