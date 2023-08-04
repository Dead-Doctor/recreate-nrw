#version 460 core

in vec2 uv;
in float height;

out vec4 FragColor;

void main()
{
    float c = (height - 35.0f) / 5.0f;
    FragColor = vec4(c, c, c, 1.0);
}