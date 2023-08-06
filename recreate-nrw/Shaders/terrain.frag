#version 460 core

in vec3 normal;

out vec4 FragColor;

uniform vec3 lightDir;

void main()
{
    float ambient = 0.3;
    float diffuse = 0.7 * max(dot(-lightDir, normal), 0.0);
    FragColor = (ambient + diffuse) * vec4(1.0);
}