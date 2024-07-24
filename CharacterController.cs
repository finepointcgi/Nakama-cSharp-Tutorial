using Godot;
using NakamacSharpTutorial;
using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;

public partial class CharacterController : CharacterBody2D
{
	public const float Speed = 300.0f;
	public const float JumpVelocity = -400.0f;

	public PlayerInfo Info;

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	public float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

    public override void _Ready()
    {
        base._Ready();
		NakamaClient.Client.PlayerDataSync += onPlayerDataSync;
    }

	public void SetupPlayer(string name, Vector2 position){
		GlobalPosition = position;
		GetNode<Label>("Label").Text = name;
	}

    private void onPlayerDataSync(string data)
    {
        var playerData = JsonConvert.DeserializeObject<PlayerSyncData>(data);

		if(playerData.Id == Name){
			GlobalPosition = playerData.Position;
			RotationDegrees = playerData.RotationDegrees;
		}
    }


    public override void _PhysicsProcess(double delta)
	{
		if(Name == NakamaClient.Session.Username){
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

			syncData();
		}
	}

	private void syncData(){
		PlayerSyncData playerSyncData = new PlayerSyncData(){
			Position = GlobalPosition,
			RotationDegrees = RotationDegrees,
			Id = Name
		};
		NakamaClient.SyncData(JsonConvert.SerializeObject(playerSyncData), 1);
	}
}
