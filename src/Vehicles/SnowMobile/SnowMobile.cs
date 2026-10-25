using Godot;
using System.Collections.Generic;

public partial class SnowMobile : RigidBody3D
{
	[ExportCategory("Przypisane Węzły")]
	[Export] private Camera3D VehicleCamera;
	[Export] private Marker3D ExitPoint;
	[Export] private Area3D InteractionArea;
	[Export] private AudioStreamPlayer3D SnowMobileSound;

	[ExportCategory("Parametry Pojazdu")]
	[Export] float EnginePower = 45.0f;
	[Export] float MaxSpeed = 28.0f;
	[Export] float SteeringSpeed = 2.0f;
	[Export] float MaxSteerAngle = 0.55f;
	[Export] float TrackGrip = 6.0f;
	[Export] float SkiGrip = 9.0f;

	[ExportCategory("Zawieszenie")]
	[Export] private float SpringRestLength = 0.6f;
	[Export] private float SpringStrength = 50.0f;
	[Export] private float SpringDamper = 6.0f;

	[ExportCategory("System Przewrócenia")]
	[Export] private float FlipCheckDelay = 1.5f;

	[ExportCategory("Ustawienia Kamery")]
	[Export] public float MouseSensitivity = 0.003f;
	[Export] public float StickSensitivity = 2.5f;

	private float FlipTimer = 0.0f;
	private float cameraPitch = 0.0f;
	private float cameraYaw = 0.0f;

	private List<RayCast3D> RayCasts = new List<RayCast3D>();

	private bool IsOccupied = false;
	private CharacterBody3D Driver = null;
	private bool IsPlayerNearby = false;
	private CharacterBody3D NearbyPlayer = null;

	private float CurrentSteerAngle = 0.0f;

	public override void _Ready() {
		CenterOfMassMode = CenterOfMassModeEnum.Custom;
		CenterOfMass = new Vector3(0, -0.6f, 0);

		RayCasts.Add(GetNode<RayCast3D>("RayFrontLeft"));
		RayCasts.Add(GetNode<RayCast3D>("RayFrontRight"));
		RayCasts.Add(GetNode<RayCast3D>("RayTrackFront"));
		RayCasts.Add(GetNode<RayCast3D>("RayTrackRear"));
		AngularDamp = 4.0f; 
   		LinearDamp = 0.5f;

		SetPhysicsProcess(false);
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (@event.IsActionPressed("Interact")) {
			if (!IsOccupied && IsPlayerNearby && NearbyPlayer != null) {
				EnterVehicle(NearbyPlayer);
			} else if (IsOccupied) {
				ExitVehicle();
			}
		}
	}

	private void Look(float yaw, float pitch) {
		VehicleCamera.RotateY(yaw);
		cameraPitch += pitch;
		cameraPitch = Mathf.Clamp(cameraPitch, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));

		Vector3 rot = VehicleCamera.Rotation;
		rot.X = cameraPitch;
		VehicleCamera.Rotation = rot;
	}

	public override void _Input(InputEvent @event) {
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured) {
			Look(-mouseMotion.Relative.X * MouseSensitivity, -mouseMotion.Relative.Y * MouseSensitivity);
		}
	}

	private void EnterVehicle(CharacterBody3D Player) {
		IsOccupied = true;
		Driver = Player;

		Driver.Visible = false;
		Driver.ProcessMode = ProcessModeEnum.Disabled;

		VehicleCamera.MakeCurrent();
		SnowMobileSound.Playing = true;

		SetPhysicsProcess(true);
	}

	private void ExitVehicle() {
		if (Driver == null) {
			return;
		}

		SetPhysicsProcess(false);
		SnowMobileSound.Playing = false;

		Driver.GlobalPosition = ExitPoint.GlobalPosition;
		Driver.GlobalRotation = new Vector3(Driver.GlobalRotation.X, GlobalRotation.Y, Driver.GlobalRotation.Z);

		Driver.Visible = true;
		Driver.ProcessMode = ProcessModeEnum.Inherit;

		Camera3D PlayerCamera = Driver.GetNode<Node3D>("Head").GetNode<Camera3D>("Camera3D");
		PlayerCamera.MakeCurrent();

		IsOccupied = false;
		Driver = null;
	}

	private void OnInteractionAreaBodyEntered(Node3D body) {
		if (body is CharacterBody3D) {
			IsPlayerNearby = true;
			NearbyPlayer = body as CharacterBody3D;
		}
	}

	private void OnInteractionAreaBodyExited(Node3D body) {
		if (body == NearbyPlayer) {
			IsPlayerNearby = false;
			NearbyPlayer = null;
		}
	}

	public override void _PhysicsProcess(double delta) {
		float d = (float)delta;
		HandleInputs(d);
		ApplySuspensionAndForces(d);
		CheckForFlip(d);

		Vector2 look = Input.GetVector("LookLeft", "LookRight", "LookUp", "LookDown");
		if (look != Vector2.Zero) {
			Look(-look.X * StickSensitivity * d, -look.Y * StickSensitivity * d);
		}
	}

	private void HandleInputs(float delta) {
		var SteerInput = Input.GetAxis("MoveRight", "MoveLeft");
		CurrentSteerAngle = Mathf.MoveToward(CurrentSteerAngle, SteerInput * MaxSteerAngle, SteeringSpeed * delta);

		if (SteerInput == 0) {
			CurrentSteerAngle = Mathf.MoveToward(CurrentSteerAngle, 0.0f, SteeringSpeed * 2.0f * delta);
		}
	}

	private void ApplySuspensionAndForces(float delta) {
		for (int i = 0; i < RayCasts.Count; i++) {
			RayCast3D rc = RayCasts[i];
			if (rc.IsColliding()) {
				Vector3 hitPoint = rc.GetCollisionPoint();
				Vector3 hitNormal = rc.GetCollisionNormal();

				Vector3 rayToHit = hitPoint - rc.GlobalPosition;
				float distance = rayToHit.Length();

				float compression = SpringRestLength - distance;
				if (compression < 0) continue;

				Vector3 pointVelocity = LinearVelocity + AngularVelocity.Cross(rc.GlobalPosition - GlobalPosition);
				float velocityAlongNormal = pointVelocity.Dot(hitNormal);

				Vector3 suspensionForce = (hitNormal * compression * SpringStrength) - (hitNormal * velocityAlongNormal * SpringDamper);
				ApplyForce(suspensionForce, rc.GlobalPosition - GlobalPosition);

				if (i >= 2) {
					Vector3 forwardDir = -GlobalTransform.Basis.Z;
					float speed = LinearVelocity.Dot(forwardDir);
					float accelInput = Input.GetAxis("Brake", "Throttle");

					if (Mathf.Abs(speed) < MaxSpeed) {
						ApplyForce(forwardDir * (accelInput * EnginePower * 0.5f), rc.GlobalPosition - GlobalPosition);
					}

					Vector3 rightDir = GlobalTransform.Basis.X;
					float lateralVelocity = pointVelocity.Dot(rightDir);
					ApplyForce(-rightDir * lateralVelocity * TrackGrip * (Mass * 0.5f), rc.GlobalPosition - GlobalPosition);
				} else {
					Vector3 skiForward = (-GlobalTransform.Basis.Z).Rotated(GlobalTransform.Basis.Y, CurrentSteerAngle);
					Vector3 rightDir = skiForward.Cross(hitNormal).Normalized();
					float lateralVelocity = pointVelocity.Dot(rightDir);

					ApplyForce(-rightDir * lateralVelocity * SkiGrip * (Mass * 0.5f), rc.GlobalPosition - GlobalPosition);
				}
			}
		}
	}

	private void CheckForFlip(float delta) {
		if (!IsOccupied) return;

		float upDot = GlobalTransform.Basis.Y.Dot(Vector3.Up);

		bool isUpsideDown = upDot < 0.1f;
		bool isMovingSlowly = LinearVelocity.Length() < 1.5f;

		if (isUpsideDown && isMovingSlowly) {
	   		FlipTimer += delta;
		
			if (FlipTimer >= FlipCheckDelay)
			{
				RightVehicle();
			}
		} else {
			FlipTimer = 0.0f;
		}

		if (Input.IsActionJustPressed("ResetVehicle")) {
			RightVehicle();
		}
	}

	private void RightVehicle() {
		Vector3 currentPos = GlobalPosition + new Vector3(0, 1.2f, 0);
	
		Vector3 currentRot = GlobalRotation;
		GlobalRotation = new Vector3(0, currentRot.Y, 0);
	
		GlobalPosition = currentPos;

		LinearVelocity = Vector3.Zero;
		AngularVelocity = Vector3.Zero;
	
		FlipTimer = 0.0f;
	}
}
