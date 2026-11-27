shader_type canvas_item;

uniform float sway_pixels = 1.0; // how many pixels the very top moves at most
uniform float canopy_bottom = 0.6; // where the leaves end and trunk begins
uniform float speed = 1.5; // how fast it sways back and forth
uniform float gust_speed = 0.4; // how often strong gust comes through

varying float phase;
varying vec4 tint;

void vertex() {
	// trees in different spots sway at different times
	phase = MODEL_MATRIX[3].x * 0.013 + MODEL_MATRIX[3].y * 0.007;
	tint = COLOR;
}

void fragment() {
	float gust = 0.5 + 0.5 * sin(TIME * gust_speed + phase * 2.0);
	float offset = round(sin(TIME * speed + phase) * sway_pixels * gust);
	if (UV.y > canopy_bottom) {
		offset = 0.0;
	}
	
	vec2 uv = UV;
	uv.x -= offset * TEXTURE_PIXEL_SIZE.x;
	vec4 c = texture(TEXTURE, uv);
	if (uv.x < 0.0 || uv.x > 1.0) {
		c.a = 0.0;
	}
	COLOR = c * tint;
}