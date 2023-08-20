#version 460 core

in vec2 uv;

out vec4 FragColor;

uniform sampler2D foliageTexture;

void main() {
    vec4 color = texture(foliageTexture, uv);
    if (color.a < 0.1) discard;
    FragColor = color;
}