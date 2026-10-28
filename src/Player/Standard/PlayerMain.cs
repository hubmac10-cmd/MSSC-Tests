using Godot;

public partial class PlayerMain : CharacterBody3D
{
	#region Zmienne
	// Eksporty
	[ExportCategory("Referencje")]
	[Export] private Node3D Head;
	[Export] private Camera3D Camera;
	[Export] private Panel StaminaBar;
	[Export] private Node3D CameraCrouchPosition;

	[ExportCategory("Rodzaje Prękdkości i Ruchu")]
	[Export] private float JumpVelocity = 4.5f;
	[Export] private float Acceleration = 0.5f;
	[Export] private float Deceleration = 0.3f;
	[ExportGroup("Prędkości")]
	[Export] private float SprintSpeed = 10.0f;
	[Export] private float StandardSpeed = 5.0f;
	[Export] private float CrouchSpeed = 2.5f;

	[ExportCategory("Ustawienia Staminy")]
	[Export] private float StaminaRegeneration = 0.05f;
	[Export] private float StaminaDrain = 0.1f;
	[Export] private float MaxStamina = 100.0f;
	[Export] private float MinStaminaToSprint = 5.0f;

	[ExportCategory("Ustawienia Kamery")]
	[Export] private float FovChange = 1.5f;
	[Export] private bool EnableTilt = true;
	[Export] private bool EnableBob = true;
	[ExportGroup("Nachylenie")]
	[Export] private float CameraTiltSpeed = 10.0f;
	[Export] private float RunPitch = 0.1f;
	[Export] private float RunRoll = 0.25f;
	[Export] private float MaxPitch = 1.0f;
	[Export] private float MaxRoll = 2.5f;
	[ExportGroup("Kiwanie")]
	[Export] private float BobFrequency = 2.0f;
	[Export] private float BobAmplitude = 0.08f;

	// AutoLoad
	private GameManager GameManager;
	private float MouseSensitivity;
	private float StickSensitivity;
	private float BaseFov;

	// Stamina i speed
	private float Speed;
	private float _stamina;
	public float Stamina {
		get => _stamina;
		set {
			_stamina = value;
			UpdateStaminaBar();
		}
	}

	// Zmienne Logiczne
	private bool IsSprinting = false;
	private bool IsCrouching = false;
	private bool IsExhausted = false;

	// Inne
	private Vector3 StandardPosition;
	private Vector3 TargetHeadPosition;
	private float TimeBob = 0.0f;
	#endregion

	public override void _Ready() {
		// AutoLoad
		GameManager = GetNode<GameManager>("/root/GameManager");
		MouseSensitivity = GameManager.MouseSensitivity;
		StickSensitivity = GameManager.StickSensitivity;
		BaseFov = GameManager.BaseFov;

		// Ustawianie domyślnych wartości
		Camera.Fov = BaseFov;
		Speed = StandardSpeed;
		Stamina = MaxStamina;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		StandardPosition = Head.Position;
	}

	private void Look(float Yaw, float Pitch) {
		RotateY(Yaw);
		Head.RotateX(Pitch);
		var rot = Head.Rotation;
		rot.X = Mathf.Clamp(rot.X, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
		Head.Rotation = rot;
	}

	private void Crouch() {
		if (IsCrouching) {
			TargetHeadPosition = CameraCrouchPosition.Position;
			Speed = CrouchSpeed;
		} else if (!IsCrouching) {
			TargetHeadPosition = StandardPosition;
			Speed = StandardSpeed;
		}
	}

	private void Sprint() {
		if (IsSprinting) {
			Speed = SprintSpeed;
			Stamina = Mathf.Max(0.0f, Stamina - StaminaDrain);

			if (Stamina <= 0.0f) {
				IsSprinting = false;
				IsExhausted = true;
			}
		} else {
			if (Stamina < MaxStamina) {
				Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegeneration);
			}
			if (IsExhausted && Stamina >= MinStaminaToSprint) {
				IsExhausted = false;
			}
		}
	}

	private void UpdateStaminaBar() {
		StaminaBar.SetDeferred(Control.PropertyName.Size, new Vector2(1920 * Stamina / MaxStamina, StaminaBar.Size.Y));
	}

	public override void _Input(InputEvent @event) {
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured) {
			Look(-mouseMotion.Relative.X * MouseSensitivity, -mouseMotion.Relative.Y * MouseSensitivity);
		}
	}

	private Vector3 HeadBob(float Time) {
		var Pos = Vector3.Zero;
		Pos.Y = Mathf.Sin(Time * BobFrequency) * BobAmplitude;
		Pos.X = Mathf.Cos(Time * BobFrequency / 2) * BobAmplitude;
		return Pos;
	}

	private void CalculateViewOffset(float Delta, Vector3 Velocity) {
		var Angles = Vector3.Zero;

		if (EnableTilt) {
			var Forward = GlobalTransform.Basis.Z;
			var Right = GlobalTransform.Basis.X;

			var ForwardDot = Velocity.Dot(Forward);
			var ForwardTilt = Mathf.Clamp(ForwardDot * Mathf.DegToRad(RunPitch), Mathf.DegToRad(-MaxPitch), Mathf.DegToRad(MaxPitch));
			Angles.X += ForwardTilt;

			var RightDot = Velocity.Dot(Right);
			var SideTilt = Mathf.Clamp(RightDot * Mathf.DegToRad(RunRoll), Mathf.DegToRad(-MaxRoll), Mathf.DegToRad(MaxRoll));
			Angles.Z -= SideTilt;
		}

		Camera.Rotation = Camera.Rotation.Lerp(Angles, CameraTiltSpeed * Delta);
	}

	public override void _PhysicsProcess(double delta) {
		Vector3 velocity = Velocity;
		float d = (float)delta;

		// Dodana grawitacja
		if (!IsOnFloor()) {
			velocity += GetGravity() * d * 1.5f;
		}

		// Zarządzanie jumpem
		if (Input.IsActionJustPressed("Jump") && IsOnFloor() && !IsCrouching) {
			velocity.Y = JumpVelocity;
		}

		// Zarządzanie sprintem
		if (Input.IsActionPressed("Shift") && IsOnFloor() && !IsCrouching && !IsExhausted) {
			IsSprinting = Stamina > 0.0f;
		}
		if (Input.IsActionJustReleased("Shift") && !IsCrouching) {
			IsSprinting = false;
		}

		// Zarządzanie crouchem
		if (Input.IsActionPressed("Crouch") && IsOnFloor() && !IsSprinting) {
			IsCrouching = true;
		}
		if (Input.IsActionJustReleased("Crouch") && !IsSprinting) {
			IsCrouching = false;
		}
		
		// Sterowanie
		Vector2 InputDir = Input.GetVector("MoveLeft", "MoveRight", "MoveForward", "MoveBackward");
		Vector3 Direction = (Transform.Basis * new Vector3(InputDir.X, 0, InputDir.Y)).Normalized();
		if (IsOnFloor()) {
			if (Direction != Vector3.Zero) {
				velocity.X = Direction.X * Speed;
				velocity.Z = Direction.Z * Speed;
			} else {
				velocity.X = Mathf.Lerp(velocity.X, Direction.X * Speed, d * 7.0f);
				velocity.Z = Mathf.Lerp(velocity.Z, Direction.Z * Speed, d * 7.0f);
			}
		} else {
			velocity.X = Mathf.Lerp(velocity.X, Direction.X * Speed, d * 3.0f);
			velocity.Z = Mathf.Lerp(velocity.Z, Direction.Z * Speed, d * 3.0f);
		}

		// Obracanie kamerą na gamepadzie
		Vector2 look = Input.GetVector("LookLeft", "LookRight", "LookUp", "LookDown");
		if (look != Vector2.Zero) {
			Look(-look.X * StickSensitivity * d, -look.Y * StickSensitivity * d);
		}

		// Funkcje do działanie crouchu i sprintu
		Crouch();
		Sprint();

		// Płynne przejście między crouchem i standard position
		Vector3 CurrentPosition = Head.Position;
		CurrentPosition.Y = Mathf.Lerp(CurrentPosition.Y, TargetHeadPosition.Y, 7.0f * d);
		Head.Position = CurrentPosition;

		// Kiwanie głową
		if (EnableBob) {
			if (IsOnFloor() && velocity.Length() > 0.1f) {
				TimeBob += d * velocity.Length();
			}
			Vector3 CamPos = Camera.Position;
			CamPos.Y = HeadBob(TimeBob).Y;
			Camera.Position = CamPos;
		}

		// Nachylenie Kamery
		CalculateViewOffset(d, velocity);

		// FOV
		var VelocityClamped = Mathf.Clamp(velocity.Length(), 0.5f, SprintSpeed * 2.0f);
		var TargetFov = BaseFov + FovChange + VelocityClamped;
		Camera.Fov = Mathf.Lerp(Camera.Fov, TargetFov, d * 8.0f);

		Velocity = velocity;
		MoveAndSlide();
	}
}
