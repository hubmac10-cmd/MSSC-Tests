using Godot;

public partial class PlayerMain : CharacterBody3D
{
	[ExportCategory("Rodzaje Prękdkości i Ruchu")]
	[Export] public float SprintSpeed = 10.0f;
	[Export] public float StandardSpeed = 5.0f;
	[Export] public float CrouchSpeed = 2.5f;
	[Export] public float JumpVelocity = 4.5f;

	[ExportCategory("Ustawienia Staminy")]
	[Export] public float StaminaRegeneration = 0.05f;
	[Export] public float StaminaDrain = 0.1f;
	[Export] public float MaxStamina = 100.0f;

	[ExportCategory("Ustawienia Kamery")]
	[Export] public float MouseSensitivity = 0.003f;
	[Export] public float StickSensitivity = 2.5f;

	private float Speed;

	private float _stamina;
	public float Stamina {
		get => _stamina;
		set { 
			_stamina = value;
			UpdateStaminaBar();
		}
	}
	private float MinStaminaToSprint;

	private bool IsSprinting = false;
	private bool IsCrouching = false;
	private bool IsExhausted = false;

	private Vector3 StandardPosition;
	private Node3D Head;
	private Panel StaminaBar;

	public override void _Ready() {
		Head = GetNode<Node3D>("Head");
		StaminaBar = GetNode<CanvasLayer>("Gui").GetNode<Panel>("StaminaBar");
		Speed = StandardSpeed;
		Stamina = MaxStamina;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		StandardPosition = Head.Position;
		MinStaminaToSprint = MaxStamina * 0.05f;
	}

	private void Look(float yaw, float pitch) {
		RotateY(yaw);
		Head.RotateX(pitch);
		var rot = Head.Rotation;
		rot.X = Mathf.Clamp(rot.X, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
		Head.Rotation = rot;
	}

	private void Crouch() {
		if (IsCrouching) {
			Vector3 NewPosition = StandardPosition;
			NewPosition.Y = 0.036f;
			Head.Position = NewPosition;
			Speed = CrouchSpeed;
		} else if (!IsCrouching) {
			Head.Position = StandardPosition;
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

	public void UpdateStaminaBar() {
		StaminaBar.Size = new Vector2(1920 * Stamina / MaxStamina, StaminaBar.Size.Y);
	}

	public override void _Input(InputEvent @event) {
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured) {
			Look(-mouseMotion.Relative.X * MouseSensitivity, -mouseMotion.Relative.Y * MouseSensitivity);
		}
	}

	public override void _PhysicsProcess(double delta) {
		Vector3 velocity = Velocity;
		float d = (float)delta;

		Speed = StandardSpeed;
		
		Crouch();	
		Sprint();
		
		if (!IsOnFloor()) {
			velocity += GetGravity() * d * 1.5f;
		}

		if (Input.IsActionJustPressed("Jump") && IsOnFloor() && !IsCrouching) {
			velocity.Y = JumpVelocity;
		}

		if (Input.IsActionPressed("Shift") && IsOnFloor() && !IsCrouching && !IsExhausted) {
			IsSprinting = Stamina > 0.0f;
		}

		if (Input.IsActionJustReleased("Shift") && !IsCrouching) {
			IsSprinting = false;
		}

		if (Input.IsActionPressed("Crouch") && IsOnFloor() && !IsSprinting) {
			IsCrouching = true;
		}

		if (Input.IsActionJustReleased("Crouch") && !IsSprinting) {
			IsCrouching = false;
		}
			
		Vector2 inputDir = Input.GetVector("MoveLeft", "MoveRight", "MoveForward", "MoveBackward");
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		if (direction != Vector3.Zero) {
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
		} 
		else {
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
		}

		Vector2 look = Input.GetVector("LookLeft", "LookRight", "LookUp", "LookDown");
		if (look != Vector2.Zero) {
			Look(-look.X * StickSensitivity * d, -look.Y * StickSensitivity * d);
		}

		Velocity = velocity;
		MoveAndSlide();
	}
}
