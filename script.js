const API_BASE = "https://localhost:44328";
/* ================= LOGIN ================= */
async function login() {
    try {
        const username = document.getElementById("username").value;
        const password = document.getElementById("password").value;

        const response = await fetch(`${API_BASE}/api/Account/login`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ username, password })
        });

        const data = await response.json();

        if (data && data.status) {
            localStorage.setItem("accessToken", data.accessToken);
            localStorage.setItem("refreshToken", data.refreshToken);
            localStorage.setItem("userRole", data.role);

            if (data.userId) {
                localStorage.setItem("userId", data.userId);
            } else {
                localStorage.setItem("userId", username);
            }

            window.location.href = "dashboard.html";
        } else {
            alert("Login failed");
        }
    } catch (error) {
        console.error("Login error:", error);
        alert("Login error");
    }
}

/* ================= LOAD USERS ================= */
async function loadUsers() {
    try {
        const token = localStorage.getItem("accessToken");
        if (!token) return;

        const response = await fetch(`${API_BASE}/api/users`, {
            method: "GET",
            headers: {
                "Authorization": `Bearer ${token}`
            }
        });

        if (!response.ok) {
            console.log("Load users failed:", response.status);
            return;
        }

        const users = await response.json();
        if (!Array.isArray(users)) return;

        const tableBody = document.getElementById("usersTableBody");
        if (!tableBody) return;

        tableBody.innerHTML = "";

        users.forEach(user => {
            const roles = Array.isArray(user.roles) ? user.roles.join(", ") : "";
            const row = `
<tr>
    <td>${user.email || ""}</td>
    <td>${roles}</td>
    <td>
        <button onclick="openEditUser('${user.id}')">✏ Edit</button>
        <button onclick="deleteUser('${user.id}')">🗑 Delete</button>
    </td>
</tr>
`;
            tableBody.insertAdjacentHTML("beforeend", row);
        });
    } catch (error) {
        console.error("Load users error:", error);
    }
}

/* ================= ADD USER ================= */
async function addUser() {
    try {
        const name = document.getElementById("newName").value.trim();
        const email = name;
        const password = document.getElementById("newPassword").value.trim();
        const role = document.getElementById("newRole").value;
        const token = localStorage.getItem("accessToken");

        if (!email) {
            alert("Please enter email");
            return;
        }
        if (password.length < 6) {
            alert("Password must be at least 6 characters");
            return;
        }

        const response = await fetch(`${API_BASE}/api/users`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Authorization": `Bearer ${token}`
            },
            body: JSON.stringify({
    userName: name,
    email,
    password,
    roleName: role
})
        });

        if (response.ok) {
            alert("User created successfully");
            closeAddUserModal();
            loadUsers();
        } else {
            const errorText = await response.text();
            console.log("Create user error:", errorText);
            alert(errorText || "Failed to create user");
        }
    } catch (error) {
        console.error("Add user error:", error);
        alert("Error while creating user");
    }
}

/* ================= OPEN EDIT USER ================= */
async function openEditUser(userId) {
    try {
        const token = localStorage.getItem("accessToken");
        const response = await fetch(`${API_BASE}/api/users/${userId}`, {
            method: "GET",
            headers: {
                "Authorization": `Bearer ${token}`
            }
        });

        if (!response.ok) {
            alert("User not found");
            return;
        }

        const user = await response.json();

        // Open modal in edit mode
        document.getElementById("addUserModal").style.display = "block";
        document.getElementById("modalTitle").innerText = "Edit User";
        document.getElementById("newName").value = user.userName || ""

        document.getElementById("newName").value = user.email || "";
        document.getElementById("newPassword").style.display = "none";

        if (user.roles && user.roles.length > 0) {
            document.getElementById("newRole").value = user.roles[0];
        }

        const btn = document.getElementById("saveUserBtn");
        btn.innerText = "Update User";
btn.onclick = () => updateUser(userId);

    } catch (error) {
        console.error("Edit user error:", error);
        alert("Error loading user data");
    }
}

/* ================= UPDATE ROLE ================= */
async function updateUserRole(userId) {
    try {
        const role = document.getElementById("newRole").value;
        const token = localStorage.getItem("accessToken");

        const response = await fetch(`${API_BASE}/api/users/${userId}/role`, {
            method: "PUT",
            headers: {
                "Content-Type": "application/json",
                "Authorization": `Bearer ${token}`
            },
            body: JSON.stringify({ roles: [role] })
        });

        if (response.ok) {
            alert("Role updated successfully");
            closeAddUserModal();
            loadUsers();
        } else {
            const error = await response.text();
            alert(error || "Failed to update role");
        }
    } catch (error) {
        console.error("Update role error:", error);
        alert("Error updating role");
    }
}
async function updateUser(userId) {

    const name = document.getElementById("newName").value.trim();
    const email = name;
    const role = document.getElementById("newRole").value;
    const token = localStorage.getItem("accessToken");

    try {

        const response = await fetch(`${API_BASE}/api/users/${userId}`, {
            method: "PUT",
            headers: {
                "Content-Type": "application/json",
                "Authorization": `Bearer ${token}`
            },
            body: JSON.stringify({
    email: name,
    password: "",
    roleName: role
})
        });

        if (!response.ok) {
            const error = await response.text();
            alert(error || "Failed to update user");
            return;
        }

        await updateUserRole(userId);

        alert("User updated successfully");

        closeAddUserModal();
        loadUsers();

    } catch (error) {
        console.error(error);
    }
}

/* ================= DELETE USER ================= */
async function deleteUser(userId) {
    try {
        if (!confirm("Are you sure you want to delete this user?")) return;

        const token = localStorage.getItem("accessToken");
        const response = await fetch(`${API_BASE}/api/users/${userId}`, {
            method: "DELETE",
            headers: {
                "Authorization": `Bearer ${token}`
            }
        });

        if (response.ok) {
            alert("User deleted successfully");
            loadUsers();
        } else {
            const error = await response.text();
            alert(error || "Failed to delete user");
        }
    } catch (error) {
        console.error("Delete user error:", error);
        alert("Error deleting user");
    }
}

/* ================= PERMISSIONS ================= */
async function loadPermissions() {
    try {
        const token = localStorage.getItem("accessToken");
        const userId = localStorage.getItem("userId");

        if (!token || !userId) return;

        const response = await fetch(`${API_BASE}/api/users/${userId}/permissions`, {
            method: "GET",
            headers: {
                "Authorization": `Bearer ${token}`
            }
        });

        if (!response.ok) {
            console.log("Permissions API failed:", response.status);
            return;
        }

        const permissions = await response.json();
        localStorage.setItem("permissions", JSON.stringify(permissions));
        applyPermissions();
    } catch (error) {
        console.error("Permissions error:", error);
    }
}

function applyPermissions() {
    const permissions = JSON.parse(localStorage.getItem("permissions") || "[]");
    const usersMenu = document.getElementById("usersMenu");

    if (usersMenu && !permissions.includes("view_users")) {
        usersMenu.style.display = "none";
    }
}

/* ================= LOGOUT ================= */
function logout() {
    localStorage.removeItem("accessToken");
    localStorage.removeItem("refreshToken");
    localStorage.removeItem("permissions");
    localStorage.removeItem("userId");
    localStorage.removeItem("userRole");
    window.location.href = "login.html";
}

/* ================= MODAL ================= */
function openAddUserModal() {
    document.getElementById("addUserModal").style.display = "block";
    document.getElementById("modalTitle").innerText = "Add New User";

    document.getElementById("newName").value = "";   // clear name
    document.getElementById("newPassword").value = "";
    document.getElementById("newRole").value = "Admin";

    document.getElementById("newPassword").style.display = "block";

    const btn = document.getElementById("saveUserBtn");
    btn.innerText = "Create User";
    btn.onclick = addUser;
}

function closeAddUserModal() {
    document.getElementById("addUserModal").style.display = "none";

    document.getElementById("newName").value = "";
    document.getElementById("newPassword").value = "";
    document.getElementById("newRole").value = "Admin";

    document.getElementById("newPassword").style.display = "block";
}

/* ================= PAGE INIT ================= */
document.addEventListener("DOMContentLoaded", function () {
    const token = localStorage.getItem("accessToken");
    const currentPath = window.location.pathname;

    if (!token && !currentPath.includes("login.html")) {
        window.location.href = "login.html";
        return;
    }

    if (currentPath.includes("users.html")) {
        loadUsers();
    }

    loadPermissions();
});