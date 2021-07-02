using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Textures
{
    public static class TextureLODInfo
    {
        public static readonly CaseInsensitiveDictionary<int> LE3MaxLodSizes = new()
        {
            ["texturegroup_ambientlightmap"] = 2048,
            ["texturegroup_apl_1024"] = 4096,
            ["texturegroup_apl_128"] = 512,
            ["texturegroup_apl_256"] = 1024,
            ["texturegroup_apl_512"] = 2048,
            ["texturegroup_character"] = 4096,
            ["texturegroup_character_1024"] = 4096,
            ["texturegroup_character_diff"] = 2048,
            ["texturegroup_character_norm"] = 2048,
            ["texturegroup_character_spec"] = 2048,
            ["texturegroup_characternormalmap"] = 4096,
            ["texturegroup_characterspecular"] = 4096,
            ["texturegroup_cinematic"] = 4096,
            ["texturegroup_effects"] = 4096,
            ["texturegroup_effectsnotfiltered"] = 4096,
            ["texturegroup_environment_1024"] = 4096,
            ["texturegroup_environment_128"] = 512,
            ["texturegroup_environment_256"] = 1024,
            ["texturegroup_environment_512"] = 2048,
            ["texturegroup_environment_64"] = 256,
            ["texturegroup_lightmap"] = 4096,
            ["texturegroup_mobileflattened"] = 4096,
            ["texturegroup_procbuilding_face"] = 1024,
            ["texturegroup_procbuilding_lightmap"] = 256,
            ["texturegroup_promotional"] = 4096,
            ["texturegroup_rendertarget"] = 4096,
            ["texturegroup_shadowmap"] = 4096,
            ["texturegroup_skybox"] = 4096,
            ["texturegroup_ui"] = 4096,
            ["texturegroup_vehicle"] = 4096,
            ["texturegroup_vehiclenormalmap"] = 4096,
            ["texturegroup_vehiclespecular"] = 4096,
            ["texturegroup_vfx_1024"] = 4096,
            ["texturegroup_vfx_128"] = 512,
            ["texturegroup_vfx_256"] = 1024,
            ["texturegroup_vfx_512"] = 2048,
            ["texturegroup_vfx_64"] = 256,
            ["texturegroup_weapon"] = 4096,
            ["texturegroup_weaponnormalmap"] = 4096,
            ["texturegroup_weaponspecular"] = 4096,
            ["texturegroup_world"] = 1024,
            ["texturegroup_worldnormalmap"] = 4096,
            ["texturegroup_worldspecular"] = 4096,
        };

        public static readonly CaseInsensitiveDictionary<int> LE2MaxLodSizes = new()
        {
            ["TEXTUREGROUP_World"] = 1024,
            ["TEXTUREGROUP_WorldNormalMap"] = 4096,
            ["TEXTUREGROUP_WorldSpecular"] = 4096,
            ["TEXTUREGROUP_Character"] = 4096,
            ["TEXTUREGROUP_CharacterNormalMap"] = 4096,
            ["TEXTUREGROUP_CharacterSpecular"] = 4096,
            ["TEXTUREGROUP_Weapon"] = 4096,
            ["TEXTUREGROUP_WeaponNormalMap"] = 4096,
            ["TEXTUREGROUP_WeaponSpecular"] = 4096,
            ["TEXTUREGROUP_Vehicle"] = 4096,
            ["TEXTUREGROUP_VehicleNormalMap"] = 4096,
            ["TEXTUREGROUP_VehicleSpecular"] = 4096,
            ["TEXTUREGROUP_Cinematic"] = 4096,
            ["TEXTUREGROUP_Effects"] = 4096,
            ["TEXTUREGROUP_EffectsNotFiltered"] = 4096,
            ["TEXTUREGROUP_Skybox"] = 4096,
            ["TEXTUREGROUP_UI"] = 4096,
            ["TEXTUREGROUP_Lightmap"] = 4096,
            ["TEXTUREGROUP_Shadowmap"] = 4096,
            ["TEXTUREGROUP_RenderTarget"] = 4096,
            ["TEXTUREGROUP_MobileFlattened"] = 4096,
            ["TEXTUREGROUP_ProcBuilding_Face"] = 1024,
            ["TEXTUREGROUP_ProcBuilding_LightMap"] = 256,
        };

        public static readonly CaseInsensitiveDictionary<int> LE1MaxLodSizes = new()
        {
            ["TEXTUREGROUP_World"] = 1024,
            ["TEXTUREGROUP_WorldNormalMap"] = 4096,
            ["TEXTUREGROUP_WorldSpecular"] = 4096,
            ["TEXTUREGROUP_Character"] = 4096,
            ["TEXTUREGROUP_CharacterNormalMap"] = 4096,
            ["TEXTUREGROUP_CharacterSpecular"] = 4096,
            ["TEXTUREGROUP_Weapon"] = 4096,
            ["TEXTUREGROUP_WeaponNormalMap"] = 4096,
            ["TEXTUREGROUP_WeaponSpecular"] = 4096,
            ["TEXTUREGROUP_Vehicle"] = 4096,
            ["TEXTUREGROUP_VehicleNormalMap"] = 4096,
            ["TEXTUREGROUP_VehicleSpecular"] = 4096,
            ["TEXTUREGROUP_Cinematic"] = 4096,
            ["TEXTUREGROUP_Effects"] = 4096,
            ["TEXTUREGROUP_EffectsNotFiltered"] = 4096,
            ["TEXTUREGROUP_Skybox"] = 4096,
            ["TEXTUREGROUP_UI"] = 4096,
            ["TEXTUREGROUP_Lightmap"] = 4096,
            ["TEXTUREGROUP_Shadowmap"] = 4096,
            ["TEXTUREGROUP_RenderTarget"] = 4096,
            ["TEXTUREGROUP_MobileFlattened"] = 4096,
            ["TEXTUREGROUP_ProcBuilding_Face"] = 1024,
            ["TEXTUREGROUP_ProcBuilding_LightMap"] = 256,
        };

        public static CaseInsensitiveDictionary<int> LEMaxLodSizes(MEGame game) => game switch
        {
            MEGame.LE1 => LE1MaxLodSizes,
            MEGame.LE2 => LE2MaxLodSizes,
            MEGame.LE3 => LE3MaxLodSizes,
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, $"{nameof(LEMaxLodSizes)} is only valid for LE games!")
        };
    }
}
