#version 460 core

in vec2 uv;

out vec4 FragColor;

uniform sampler2D imageTexture;

void main() {
    FragColor = texture(imageTexture, uv);
}