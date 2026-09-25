using UnityEngine;

namespace Assets.Scripts.Effects {
    public class ShaderCache : MonoBehaviour {
        private static ShaderCache instance;
        public static ShaderCache Instance {
            get {
                if (instance == null)
                    instance = GameObject.FindObjectOfType<ShaderCache>();

                return instance;
            }
        }

        public Shader SpriteShader;
        public Shader AlphaBlendParticleShader;
        public Shader AdditiveShader;

        private void Awake() {
            // The map scene holds one and is loaded again after going back to character select
            if (Instance != this) {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(this);
        }
    }
}
