extends SceneTree

func is_negative_zero(value: float) -> bool:
    var bytes = PackedByteArray()
    bytes.resize(8)
    bytes.encode_double(0, value)
    return value == 0.0 and bytes.decode_s64(0) < 0

func _initialize():
    var node_script = load("res://scripts/generated/LifecycleProbe.cs")
    var instance = node_script.new()
    var observer = node_script.new()
    root.add_child(instance)
    await process_frame
    var valid = instance.get("EnterCount") == 1 and instance.get("ReadyCount") == 1
    valid = valid and node_script.get_property_default_value("Offset") == Vector2(12.0, 34.0)
    valid = valid and instance.get("Offset") == Vector2(12.0, 34.0)
    valid = valid and node_script.get_property_default_value("NegativeSpeed") == -12.5
    valid = valid and instance.get("NegativeSpeed") == -12.5
    valid = valid and node_script.get_property_default_value("NegativeMode") == -1
    valid = valid and instance.get("NegativeMode") == -1
    valid = valid and is_negative_zero(node_script.get_property_default_value("NegativeZero"))
    valid = valid and is_negative_zero(instance.get("NegativeZero"))
    root.remove_child(instance)
    valid = valid and instance.get("ExitCount") == 1 and observer.get("DisposedCount") == 0
    root.add_child(instance)
    valid = valid and instance.get("EnterCount") == 2 and instance.get("ReadyCount") == 1
    root.remove_child(instance)
    instance.request_ready()
    root.add_child(instance)
    valid = valid and instance.get("EnterCount") == 3 and instance.get("ReadyCount") == 2
    instance.free()
    valid = valid and observer.get("DisposedCount") == 1 and observer.get("OrderValid")

    var never_entered = node_script.new()
    never_entered.free()
    valid = valid and observer.get("DisposedCount") == 2
    var borrowed = load("res://scripts/generated/BorrowedProbe.cs").new()
    borrowed.free()
    valid = valid and observer.get("DisposedCount") == 2
    var helper = load("res://scripts/generated/HelperProbe.cs").new()
    root.add_child(helper)
    valid = valid and helper.get("Valid")
    helper.free()
    observer.free()
    if not valid:
        printerr("Runtime integration contract failed: export defaults, lifecycle/disposal, or implementation lookup.")
        quit(1)
        return
    print("RUNTIME_INTEGRATION_OK")
    quit(0)
