#version 460 core

in vec3 position;
in vec3 normal;
in vec2 UV;

out vec4 FragColor;

uniform float time;
uniform vec3 lightPosView;

uniform sampler2D container;
uniform sampler2D awesomeface;

float ambient = 0.3;
float diffuse = 0.5;
float specular = 0.5;
float shininess = 6.0;

vec4 lightColor = vec4(1.0, 1.0, 1.0, 1.0);

void main()
{    
    vec4 color = mix(mix(texture(container, UV), texture(awesomeface, UV), 0.2), vec4(UV, 1.0, 1.0), (sin(time) + 1.0) / 2.0);
    
    vec3 lightDir = normalize(position - lightPosView);
    vec3 viewDir = normalize(position);
    
    //ambient
    vec4 ambientColor = ambient * color * lightColor;
    
    //diffuse
    vec4 diffuseColor = diffuse * (max(dot(-lightDir, normal), 0.0) * color * lightColor);
    
    //specular
    vec3 reflection = reflect(-lightDir, normal);
    vec4 specularColor = specular * (pow(max(dot(viewDir, reflection), 0.0), pow(2.0, shininess)) * lightColor);
    
    FragColor = ambientColor + diffuseColor + specularColor;
}