import * as THREE from 'three';

const TAU = Math.PI * 2;
const WATER_Y = 0.065;

function mesh(geometry, material, parent, x = 0, y = 0, z = 0) {
  const object = new THREE.Mesh(geometry, material);
  object.position.set(x, y, z);
  object.castShadow = true;
  object.receiveShadow = true;
  parent.add(object);
  return object;
}

function disposeGroup(group) {
  const geometries = new Set();
  const materials = new Set();
  group.traverse((object) => {
    if (object.geometry) geometries.add(object.geometry);
    if (object.material) {
      for (const material of Array.isArray(object.material) ? object.material : [object.material]) {
        materials.add(material);
      }
    }
  });
  geometries.forEach((geometry) => geometry.dispose());
  materials.forEach((material) => material.dispose());
  group.removeFromParent();
}

/** A traveler whose origin is at the soles and whose forward direction is +Z. */
export function createCharacter(scene) {
  const group = new THREE.Group();
  group.name = 'The lantern keeper';
  scene.add(group);
  const body = new THREE.Group();
  group.add(body);

  const cloak = new THREE.MeshStandardMaterial({ color: 0x527985, roughness: 0.97, flatShading: true, side: THREE.DoubleSide });
  const shoulderCloth = new THREE.MeshStandardMaterial({ color: 0xc1d4d0, roughness: 0.95, flatShading: true });
  const hoodCloth = new THREE.MeshStandardMaterial({ color: 0x9db9bf, roughness: 0.92, flatShading: true });
  const lining = new THREE.MeshStandardMaterial({ color: 0x14262d, roughness: 1, flatShading: true });
  const leather = new THREE.MeshStandardMaterial({ color: 0x39332c, roughness: 0.98, flatShading: true });
  const faceMaterial = new THREE.MeshStandardMaterial({ color: 0xc3bca2, roughness: 1, flatShading: true });
  const brass = new THREE.MeshStandardMaterial({ color: 0xb69b58, roughness: 0.65, metalness: 0.25, flatShading: true });
  const wood = new THREE.MeshStandardMaterial({ color: 0x655443, roughness: 1, flatShading: true });
  const lampMaterial = new THREE.MeshBasicMaterial({ color: 0xffd28b, transparent: true, opacity: 0.95 });

  // Short stride beneath the long coat makes motion readable from above.
  const leftLeg = new THREE.Group();
  const rightLeg = new THREE.Group();
  leftLeg.position.set(0.115, 0.51, 0);
  rightLeg.position.set(-0.115, 0.51, 0);
  body.add(leftLeg, rightLeg);
  for (const leg of [leftLeg, rightLeg]) {
    mesh(new THREE.CylinderGeometry(0.074, 0.058, 0.34, 5), lining, leg, 0, -0.17, 0);
    const boot = mesh(new THREE.BoxGeometry(0.145, 0.13, 0.24), leather, leg, 0, -0.425, 0.045);
    boot.rotation.x = 0.06;
  }

  mesh(new THREE.CylinderGeometry(0.24, 0.2, 0.56, 7), lining, body, 0, 0.88, 0);
  const shoulders = mesh(new THREE.SphereGeometry(0.31, 8, 5), shoulderCloth, body, 0, 1.145, -0.01);
  shoulders.scale.set(1.17, 0.52, 0.81);

  // A three-row, open-front cloak. The vertices are retained for gentle cloth motion.
  const vertices = [];
  const indices = [];
  const segments = 12;
  const rows = [
    { radius: 0.27, y: 1.17, z: -0.01 },
    { radius: 0.30, y: 0.74, z: -0.025 },
    { radius: 0.41, y: 0.28, z: -0.065 },
  ];
  for (let row = 0; row < rows.length; row++) {
    for (let index = 0; index <= segments; index++) {
      const angle = 0.4 + (TAU - 0.8) * (index / segments);
      const { radius, y, z } = rows[row];
      const scallop = row === 2 ? Math.cos(index * 1.55) * 0.035 : 0;
      vertices.push(Math.sin(angle) * radius, y + scallop, Math.cos(angle) * radius * 0.72 + z);
      if (row < rows.length - 1 && index < segments) {
        const a = row * (segments + 1) + index;
        const b = a + segments + 1;
        indices.push(a, b, a + 1, a + 1, b, b + 1);
      }
    }
  }
  const capeGeometry = new THREE.BufferGeometry();
  capeGeometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
  capeGeometry.setIndex(indices);
  capeGeometry.computeVertexNormals();
  const cape = mesh(capeGeometry, cloak, body);
  cape.name = 'Travel-worn cloak';
  const capeBase = Float32Array.from(vertices);

  const head = new THREE.Group();
  head.position.set(0, 1.45, 0.005);
  body.add(head);
  const hood = mesh(new THREE.SphereGeometry(0.263, 8, 6), hoodCloth, head, 0, 0, -0.025);
  hood.scale.set(0.95, 1.1, 0.96);
  const hoodShadow = mesh(new THREE.SphereGeometry(0.19, 8, 5), lining, head, 0, -0.018, 0.17);
  hoodShadow.scale.set(0.9, 1.07, 0.38);
  const face = mesh(new THREE.SphereGeometry(0.118, 6, 5), faceMaterial, head, 0, -0.036, 0.224);
  face.scale.set(0.76, 1.06, 0.3);
  // The heavy brow leaves just a small pale face beneath the hood.
  const brow = mesh(new THREE.BoxGeometry(0.25, 0.068, 0.095), hoodCloth, head, 0, 0.103, 0.18);
  brow.rotation.x = -0.12;

  // Quiet copper details help the silhouette survive the game's text renderer.
  mesh(new THREE.OctahedronGeometry(0.05), brass, body, 0.13, 1.17, 0.235);
  const shoulderStrap = mesh(new THREE.BoxGeometry(0.055, 0.49, 0.025), leather, body, -0.035, 0.98, 0.233);
  shoulderStrap.rotation.z = -0.43;
  const backPack = mesh(new THREE.BoxGeometry(0.25, 0.30, 0.13), leather, body, 0, 0.98, -0.26);
  backPack.rotation.x = -0.07;
  mesh(new THREE.BoxGeometry(0.028, 0.21, 0.02), brass, body, 0, 1.005, -0.335);

  const freeArm = new THREE.Group();
  freeArm.position.set(0.29, 1.085, 0);
  body.add(freeArm);
  const sleeve = mesh(new THREE.CylinderGeometry(0.095, 0.075, 0.37, 5), cloak, freeArm, 0.02, -0.18, 0);
  sleeve.rotation.z = 0.12;
  mesh(new THREE.SphereGeometry(0.063, 5, 4), leather, freeArm, 0.043, -0.385, 0.018);

  const staffArm = new THREE.Group();
  staffArm.position.set(-0.29, 1.09, 0);
  body.add(staffArm);
  const staffSleeve = mesh(new THREE.CylinderGeometry(0.1, 0.073, 0.32, 5), cloak, staffArm, -0.02, -0.14, 0.07);
  staffSleeve.rotation.x = -0.34;
  staffSleeve.rotation.z = -0.2;
  mesh(new THREE.SphereGeometry(0.065, 5, 4), leather, staffArm, -0.075, -0.29, 0.14);

  const staff = new THREE.Group();
  staff.position.set(-0.085, -0.29, 0.15);
  staffArm.add(staff);
  mesh(new THREE.CylinderGeometry(0.021, 0.025, 1.51, 5), wood, staff, 0, 0.0, 0);
  mesh(new THREE.CylinderGeometry(0.027, 0.027, 0.12, 6), brass, staff, 0, -0.67, 0);
  const crook = mesh(new THREE.TorusGeometry(0.10, 0.021, 4, 8, Math.PI * 1.28), wood, staff, 0.097, 0.73, 0);
  crook.rotation.z = -0.28;

  const lantern = new THREE.Group();
  lantern.position.set(0.17, 0.54, 0);
  staff.add(lantern);
  mesh(new THREE.TorusGeometry(0.035, 0.009, 4, 8), brass, lantern, 0, 0.11, 0);
  mesh(new THREE.CylinderGeometry(0.065, 0.065, 0.15, 6), lampMaterial, lantern, 0, 0, 0);
  mesh(new THREE.ConeGeometry(0.09, 0.06, 6), brass, lantern, 0, 0.1, 0);
  mesh(new THREE.CylinderGeometry(0.075, 0.065, 0.035, 6), brass, lantern, 0, -0.09, 0);
  for (let i = 0; i < 4; i++) {
    const angle = (i / 4) * TAU;
    mesh(new THREE.CylinderGeometry(0.008, 0.008, 0.16, 4), brass, lantern, Math.sin(angle) * 0.067, 0, Math.cos(angle) * 0.067);
  }
  const lampLight = new THREE.PointLight(0xffc56d, 0.8, 4.8, 2);
  lantern.add(lampLight);

  let gait = 0;
  let motion = 0;
  let clock = 0;
  return {
    group,
    lantern,
    lampLight,
    setPosition(x, y, z) {
      group.position.set(x, y, z);
    },
    update(dt, { moving = false, sprinting = false, inWater = false, facing = group.rotation.y, time } = {}) {
      const step = Math.min(Math.max(dt, 0), 0.1);
      clock = Number.isFinite(time) ? time : clock + step;
      motion = THREE.MathUtils.damp(motion, moving ? 1 : 0, 12, step);
      gait += step * (sprinting ? 12.5 : inWater ? 6.8 : 8.7);
      const stride = Math.sin(gait) * motion * (sprinting ? 0.68 : 0.44);
      leftLeg.rotation.x = stride;
      rightLeg.rotation.x = -stride;
      freeArm.rotation.x = -stride * 0.8;
      staffArm.rotation.x = stride * 0.20 - 0.03;
      staffArm.rotation.z = Math.sin(gait + 0.7) * motion * 0.035;
      body.position.y = Math.abs(Math.sin(gait)) * motion * (sprinting ? 0.065 : 0.035) + Math.sin(clock * 1.7) * 0.006;
      body.rotation.x = THREE.MathUtils.damp(body.rotation.x, sprinting && moving ? 0.10 : 0, 8, step);
      body.rotation.z = Math.sin(gait) * motion * 0.025;
      head.rotation.y = Math.sin(clock * 0.45) * (1 - motion) * 0.075;
      lantern.rotation.z = -Math.sin(gait - 0.5) * motion * 0.10 + Math.sin(clock * 1.8) * 0.025;
      lampLight.intensity = 0.78 + Math.sin(clock * 7.8) * 0.05 + Math.sin(clock * 13.1) * 0.035;
      lampMaterial.opacity = 0.88 + Math.sin(clock * 5.4) * 0.10;
      if (Number.isFinite(facing)) group.rotation.y = facing;

      const positions = capeGeometry.attributes.position;
      for (let row = 0; row < rows.length; row++) {
        const influence = row / (rows.length - 1);
        for (let index = 0; index <= segments; index++) {
          const vertex = row * (segments + 1) + index;
          const offset = vertex * 3;
          const sway = Math.sin(clock * 2.4 + index * 0.65) * 0.014 + Math.sin(gait - row * 0.6 + index * 0.28) * motion * 0.029;
          positions.setXYZ(vertex,
            capeBase[offset] + sway * influence,
            capeBase[offset + 1] + Math.cos(gait * 2 + index * 0.8) * motion * influence * 0.012,
            capeBase[offset + 2] - motion * influence * 0.045 + Math.sin(clock * 2.7 + index * 0.5) * influence * 0.015,
          );
        }
      }
      positions.needsUpdate = true;
      capeGeometry.computeVertexNormals();
    },
    dispose() {
      disposeGroup(group);
    },
  };
}

/** Bounded pools: repeated footsteps and pickups never add scene objects. */
export function createEffects(scene) {
  const group = new THREE.Group();
  group.name = 'Water ripples and motes';
  scene.add(group);

  const ringGeometry = new THREE.RingGeometry(0.88, 1, 48);
  const ripples = Array.from({ length: 24 }, (_, index) => {
    const material = new THREE.MeshBasicMaterial({ color: 0x8ccac5, transparent: true, opacity: 0, side: THREE.DoubleSide, depthWrite: false });
    const object = new THREE.Mesh(ringGeometry, material);
    object.rotation.x = -Math.PI / 2;
    object.position.y = WATER_Y + (index % 3) * 0.001;
    object.visible = false;
    object.renderOrder = 3;
    group.add(object);
    return { object, life: 0, duration: 2.2, start: 0.08 };
  });
  let rippleCursor = 0;

  const capacity = 240;
  const positions = new Float32Array(capacity * 3);
  const colors = new Float32Array(capacity * 3);
  const particleGeometry = new THREE.BufferGeometry();
  const positionAttribute = new THREE.BufferAttribute(positions, 3).setUsage(THREE.DynamicDrawUsage);
  const colorAttribute = new THREE.BufferAttribute(colors, 3).setUsage(THREE.DynamicDrawUsage);
  particleGeometry.setAttribute('position', positionAttribute);
  particleGeometry.setAttribute('color', colorAttribute);
  const particleMaterial = new THREE.PointsMaterial({ size: 0.065, vertexColors: true, transparent: true, opacity: 0.95, depthWrite: false, blending: THREE.AdditiveBlending, sizeAttenuation: true });
  const particles = new THREE.Points(particleGeometry, particleMaterial);
  particles.frustumCulled = false;
  group.add(particles);
  const pool = Array.from({ length: capacity }, () => ({ life: 0, duration: 1, x: 0, y: -1000, z: 0, vx: 0, vy: 0, vz: 0, r: 0, g: 0, b: 0 }));
  let particleCursor = 0;
  const colorValue = new THREE.Color();
  for (let i = 0; i < capacity; i++) positions[i * 3 + 1] = -1000;

  function emit(x, y, z, color, amount, speed = 1) {
    colorValue.set(color);
    for (let i = 0; i < amount; i++) {
      const particle = pool[particleCursor];
      particleCursor = (particleCursor + 1) % capacity;
      const angle = Math.random() * TAU;
      const velocity = (0.18 + Math.random() * 0.65) * speed;
      Object.assign(particle, {
        x, y, z,
        vx: Math.cos(angle) * velocity,
        vy: (0.4 + Math.random() * 0.8) * speed,
        vz: Math.sin(angle) * velocity,
        life: 0.7 + Math.random() * 0.7,
        r: colorValue.r, g: colorValue.g, b: colorValue.b,
      });
      particle.duration = particle.life;
    }
  }

  return {
    group,
    ripple(x, z) {
      const ripple = ripples[rippleCursor];
      rippleCursor = (rippleCursor + 1) % ripples.length;
      ripple.life = ripple.duration;
      ripple.object.position.x = x;
      ripple.object.position.z = z;
      ripple.object.scale.setScalar(ripple.start);
      ripple.object.material.opacity = 0.58;
      ripple.object.visible = true;
    },
    burst(x, y, z, color = 0xf7cf84) {
      emit(x, y, z, color, 30, 1);
      emit(x, y, z, 0x8fe1ce, 9, 0.65);
    },
    update(dt, time = 0) {
      const step = Math.min(Math.max(dt, 0), 0.1);
      for (const ripple of ripples) {
        if (ripple.life <= 0) continue;
        ripple.life -= step;
        const remaining = Math.max(0, ripple.life / ripple.duration);
        ripple.object.scale.setScalar(ripple.start + (1 - remaining) * 1.65);
        ripple.object.material.opacity = remaining * remaining * 0.58;
        ripple.object.visible = ripple.life > 0;
      }
      for (let i = 0; i < capacity; i++) {
        const particle = pool[i];
        const offset = i * 3;
        if (particle.life <= 0) {
          positions[offset + 1] = -1000;
          continue;
        }
        particle.life -= step;
        particle.vy -= step * 0.5;
        particle.x += particle.vx * step;
        particle.y += particle.vy * step;
        particle.z += particle.vz * step;
        const fade = Math.max(0, particle.life / particle.duration);
        const twinkle = 0.78 + Math.sin(time * 11 + i * 2.4) * 0.22;
        positions[offset] = particle.x;
        positions[offset + 1] = particle.y;
        positions[offset + 2] = particle.z;
        colors[offset] = particle.r * fade * twinkle;
        colors[offset + 1] = particle.g * fade * twinkle;
        colors[offset + 2] = particle.b * fade * twinkle;
      }
      positionAttribute.needsUpdate = true;
      colorAttribute.needsUpdate = true;
    },
    dispose() {
      disposeGroup(group);
    },
  };
}
