shader_type canvas_item;

// Hover outline for Clickable. Draws a solid border around the sprite's visuals until it goes off
uniform bool enabled = false;
uniform vec4 outline_color : source_color = vec4(1.0, 0.85, 0.2, 1.0);
uniform float width : hint_range(0.0, 8.0) = 1.0;

void fragment() {
	vec4 color = COLOR;
	if (enabled && color.a < 0.5) {
		vec2 offset = TEXTURE_PIXEL_SIZE * width;
		float neighbor = texture(TEXTURE, UV + vec2(offset.x, 0.0)).a;
		neighbor = max(neighbor, texture(TEXTURE, UV - vec2(offset.x, 0.0)).a);
		neighbor = max(neighbor, texture(TEXTURE, UV + vec2(0.0, offset.y)).a);
		neighbor = max(neighbor, texture(TEXTURE, UV - vec2(0.0, offset.y)).a);
		if (neighbor > 0.5) {
			color = outline_color;
		}
	}
	COLOR = color;
}
