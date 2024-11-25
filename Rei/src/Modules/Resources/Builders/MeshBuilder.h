#pragma once
#include "assimp/mesh.h"
#include "assimp/scene.h"
#include "Modules/Render/Mesh/Mesh.h"

class MeshBuilder
{
public:
    void BuildMeshAsset(const std::filesystem::path& assetPath, rei::resources::BinaryWriter& writer) const;
    
private:
    rei::render::Mesh ProcessMesh(const aiMesh* mesh) const;
    void ProcessNode(const aiNode* node, const aiScene* scene, std::vector<rei::render::Mesh>& meshes) const;
};
