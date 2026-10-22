using Godot;

public partial class PlayerMain : CharacterBody3D
{
	[Export] public float SprintSpeed = 10.0f;
	[Export] public float StandardSpeed = 5.0f;
	[Export] public float StaminaRegeneration = 1.0f;
	[Export] public float StaminaDrain = 1.0f;
	[Export] public float JumpVelocity = 4.5f;
	[Export] public float MaxStamina = 1000.0f;
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

	private bool IsSprinting = false;

	private Node3D Head;
	private Panel StaminaBar;

	public override void _Ready() {
		Head = GetNode<Node3D>("Head");
		StaminaBar = GetNode<CanvasLayer>("Gui").GetNode<Panel>("StaminaBar");
		Speed = StandardSpeed;
		Stamina = MaxStamina;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	private void Look(float yaw, float pitch) {
		RotateY(yaw);
		Head.RotateX(pitch);
		var rot = Head.Rotation;
		rot.X = Mathf.Clamp(rot.X, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
		Head.Rotation = rot;
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

		if (IsSprinting && IsOnFloor()) {
			Speed = SprintSpeed;
			Stamina = Mathf.Max(0.0f, Stamina - StaminaDrain);

			if (Stamina <= 0.0f) {
				IsSprinting = false;
			}
		} else if (!IsSprinting && Stamina < MaxStamina) {
			Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegeneration);
		}
		
		if (!IsOnFloor()) {
			velocity += GetGravity() * d * 2;
		}

		if (Input.IsActionJustPressed("jump") && IsOnFloor()) {
			velocity.Y = JumpVelocity;
		}

		if (Input.IsActionJustPressed("Shift") && IsOnFloor()) {
			IsSprinting = Stamina > 0.0f;
		}

		if (Input.IsActionJustReleased("Shift")) {
			IsSprinting = false;
		}

		Vector2 inputDir = Input.GetVector("left", "right", "forward", "backward");
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		if (direction != Vector3.Zero) {
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
		} 
		else {
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
		}

		Vector2 look = Input.GetVector("look_left", "look_right", "look_up", "look_down");
		if (look != Vector2.Zero) {
			Look(-look.X * StickSensitivity * d, -look.Y * StickSensitivity * d);
		}

		Velocity = velocity;
		MoveAndSlide();
	}
}
