#version 460 core

in vec3 normal;

out vec4 FragColor;

uniform vec3 lightDir;

void main()
{
    FragColor = max(dot(-lightDir, normal), 0.0) * vec4(1.0);
}