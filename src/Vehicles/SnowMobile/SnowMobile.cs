using Godot;
using Godot.Collections;
using Src.Autoload;

public partial class SnowMobile : RigidBody3D
{
	#region Zmienne
	// Eksporty
	[ExportCategory("Referencje")]
	[Export] private Node3D Head;
	[Export] private Camera3D Camera;
	[Export] private Marker3D ExitPoint;
	[Export] private Node3D CenterOfMassNode;
	[Export] private Node3D SteeringNode;
	[Export] private Array<RayCast3D> RayCasts;

	[ExportCategory("Ustawienia Kamery")]
	[Export] private float FovChange = 1.0f;
	[Export] private bool EnableTilt = true;
	[ExportGroup("Nachylenie")]
	[Export] private float CameraTiltSpeed = 10.0f;
	[Export] private float DriveRoll = 0.25f;
	[Export] private float MaxRoll = 2.5f;

	[ExportCategory("Parametry Pojazdu")]
	[Export] public float EnginePower = 6000f;
	[Export] public float MaxSpeed = 25f;
	[Export] public float TurnTorque = 2500f;
	[Export] public float LateralGrip = 4f;

	[ExportCategory("Kierownica")]
	[Export] private float MaxSteerAngle = 30.0f;
	[Export] private float SteerReturnSpeed = 10.0f;

	[ExportCategory("Zawieszenie")]
	[Export] public float RestLength = 0.6f;
	[Export] public float SpringStrength = 40f;
	[Export] public float SpringDamping = 8f;  

	[ExportCategory("System Przewrócenia")]
	[Export] private float FlipCheckDelay = 2.0f;
	[Export] private float FlipTimer = 1.5f;

	// AutoLoad
	private float MouseSensitivity;
	private float StickSensitivity;
	private float BaseFov;

	// Kamera
	private float CameraPitch = 0.0f;
	private float CameraYaw = 0.0f;

	// Wchodzenie/wychodzenie
	private bool IsOccupied = false;
	private CharacterBody3D Driver = null;
	private bool IsPlayerNearby = false;
	private CharacterBody3D NearbyPlayer = null;

	// Inne
	private float FlipCheckTimer = 0.0f;
	private float CurrentSteerAngle = 0.0f;
	#endregion

	public override void _Ready() {
		// AutoLoad
		MouseSensitivity = GameManager.Instance.MouseSensitivity;
		StickSensitivity = GameManager.Instance.StickSensitivity;
		BaseFov = GameManager.Instance.BaseFov;

		CenterOfMassMode = CenterOfMassModeEnum.Custom;
		CenterOfMass = CenterOfMassNode.Position;

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

	private void Look(float Yaw, float Pitch) {
		Head.RotateY(Yaw);
		CameraPitch += Pitch;
		CameraPitch = Mathf.Clamp(CameraPitch, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));

		Vector3 rot = Head.Rotation;
		rot.X = CameraPitch;
		Head.Rotation = rot;
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

		Camera.MakeCurrent();

		SetPhysicsProcess(true);
	}

	private void ExitVehicle() {
		if (Driver == null) {
			return;
		}

		SetPhysicsProcess(false);

		Driver.GlobalPosition = ExitPoint.GlobalPosition;
		Driver.GlobalRotation = new Vector3(Driver.GlobalRotation.X, GlobalRotation.Y, Driver.GlobalRotation.Z);

		Driver.Visible = true;
		Driver.ProcessMode = ProcessModeEnum.Inherit;

		Camera3D PlayerCamera = Driver.GetNode<Node3D>("Head").GetNode<Camera3D>("Camera");
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

	private void CalculateViewOffset(float Delta, Vector3 Velocity) {
		var Angles = Vector3.Zero;

		if (EnableTilt) {
			var Right = GlobalTransform.Basis.X;

			var RightDot = Velocity.Dot(Right);
			var SideTilt = Mathf.Clamp(RightDot * Mathf.DegToRad(DriveRoll) * 5.0f, Mathf.DegToRad(-MaxRoll), Mathf.DegToRad(MaxRoll));
			Angles.Z += SideTilt;
		}

		Camera.Rotation = Camera.Rotation.Lerp(Angles, CameraTiltSpeed * Delta);
	}

	public override void _PhysicsProcess(double delta) {
		float d = (float)delta;
		CheckForFlip(d);

		// Obracanie kamerą na gamepadzie
		Vector2 look = Input.GetVector("LookLeft", "LookRight", "LookUp", "LookDown");
		if (look != Vector2.Zero) {
			Look(-look.X * StickSensitivity * d, -look.Y * StickSensitivity * d);
		}

		// Sterowanie
		Basis Basis = GlobalTransform.Basis;

		int FrontGrounded = 0;
		int RearGrounded = 0;
		Vector3 NormalSum = Vector3.Zero;

		for (int i = 0; i < RayCasts.Count; i++) {
			RayCast3D Ray = RayCasts[i];
			if (!Ray.IsColliding())
				continue;

			Vector3 Hit = Ray.GetCollisionPoint();
			Vector3 Normal = Ray.GetCollisionNormal();
			float Distance = Ray.GlobalPosition.DistanceTo(Hit);
			float Compression = RestLength - Distance;

			Vector3 Offset = Ray.GlobalPosition - GlobalPosition;
			Vector3 PointVelocity = LinearVelocity + AngularVelocity.Cross(Offset);
			float SpringVelocity = Normal.Dot(PointVelocity);

			float Force = (Compression * SpringStrength - SpringVelocity * SpringDamping) * Mass / RayCasts.Count;
			Force = Mathf.Max(Force, 0f);

			ApplyForce(Normal * Force, Offset);

			NormalSum += Normal;
			if (i < 2) FrontGrounded++; else RearGrounded++;
		}

		int Grounded = FrontGrounded + RearGrounded;
		if (Grounded == 0 && !IsOccupied)
			return;

		Vector3 GroundNormal = NormalSum.Normalized();
		Vector3 Forward = (-Basis.Z).Slide(GroundNormal).Normalized();
		Vector3 Right = Basis.X.Slide(GroundNormal).Normalized();

		float Throttle = Input.GetAxis("Brake", "Throttle");
		float Steer = Input.GetAxis("MoveRight", "MoveLeft");
		float ForwardSpeed = LinearVelocity.Dot(Forward);

		float Traction = RearGrounded / 2f;
		if (Mathf.Abs(ForwardSpeed) < MaxSpeed || Mathf.Sign(ForwardSpeed) != Mathf.Sign(Throttle))
			ApplyCentralForce(Forward * Throttle * EnginePower * Traction);

		float lateralSpeed = LinearVelocity.Dot(Right);
		ApplyCentralForce(-Right * lateralSpeed * LateralGrip * Mass * (Grounded / 4f));

		float SkiContact = FrontGrounded / 2f;
		float SpeedFactor = Mathf.Clamp(ForwardSpeed / 10f, -1f, 1f);
		ApplyTorque(GroundNormal * Steer * TurnTorque * SpeedFactor * SkiContact);

		// Nachylenie Kamery
		CalculateViewOffset(d, LinearVelocity);
		
		// Kierownica
		float TargetAngle = Steer * Mathf.DegToRad(MaxSteerAngle);
		CurrentSteerAngle = Mathf.Lerp(CurrentSteerAngle, TargetAngle, d * SteerReturnSpeed);

		Vector3 wheelRot = SteeringNode.Rotation;
		wheelRot.Y = CurrentSteerAngle; 
		SteeringNode.Rotation = wheelRot;

		// FOV
		var VelocityClamped = Mathf.Clamp(LinearVelocity.Length(), 0.5f, MaxSpeed * 2.0f);
		var TargetFov = BaseFov + FovChange + VelocityClamped;
		Camera.Fov = Mathf.Lerp(Camera.Fov, TargetFov, d * 8.0f);
	}

	private void CheckForFlip(float delta) {
		if (!IsOccupied) return;

		float UpDot = GlobalTransform.Basis.Y.Dot(Vector3.Up);

		bool IsUpsideDown = UpDot < 0.1f;
		bool IsMovingSlowly = LinearVelocity.Length() < 1.5f;

		if (IsUpsideDown && IsMovingSlowly) {
	   		FlipCheckTimer += delta;
		
			if (FlipCheckTimer >= FlipCheckDelay)
			{
				RightVehicle();
			}
		} else {
			FlipCheckTimer = 0.0f;
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
	
		FlipCheckTimer = 0.0f;
	}
}
