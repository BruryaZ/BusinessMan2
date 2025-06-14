"use client"
import { useContext, useState } from "react"
import axios from "axios"
import type { AdminRegister } from "../models/AdminRegister"
import { useNavigate } from "react-router-dom"
import { validationSchemaUserLogin } from "../utils/validationSchema"
import { globalContext } from "../context/GlobalContext"
import { Form, Input, Button, Typography, Card, Alert, Space, Avatar, Divider, ConfigProvider } from "antd"
import { MailOutlined, LockOutlined, LoginOutlined, ExclamationCircleOutlined, CrownOutlined } from "@ant-design/icons"
import CenteredLayout from "./CenteredLayout"

const { Title, Text, Link } = Typography

const UserLogin = () => {
  const nav = useNavigate()
  const validationSchema = validationSchemaUserLogin
  const [userLogin, setUserLogin] = useState<AdminRegister>({ email: "", password: "" })
  const [errors, setErrors] = useState<string[]>([])
  const [loading, setLoading] = useState(false)
  const url = import.meta.env.VITE_API_URL
  const globalContextDetails = useContext(globalContext)

  const handleSubmit = async () => {
    setLoading(true)
    try {
      const valid = await validationSchema.isValid(userLogin)
      setErrors([])

      if (valid) {

        try {
          const { data } = await axios.post<any>(`${url}/Auth/user-login`, userLogin, { withCredentials: true })
          globalContextDetails.setUserCount(data.user.businessUsersCount)
          globalContextDetails.setUser(data.user)
          nav("/")
        }
        catch (e) {
          if (axios.isAxiosError(e) && e.response?.status === 400) {
            setErrors(["משתמש לא נמצא, נא לבדוק את האימייל והסיסמה"]);
            return;
          }
          setErrors(e instanceof Error ? [e.message] : ["שגיאה בכניסה, נא לנסות שוב מאוחר יותר"]);
          return;
        }
      }
      else {
        const validationErrors = await validationSchema.validate(userLogin).catch((err) => err.errors);
        setErrors(validationErrors || []);
      }
    } catch (e) {
      if (axios.isAxiosError(e) && e.response?.status === 400) {
        setErrors(["משתמש לא נמצא, נא לבדוק את האימייל והסיסמה"]);
        return;
      }
      setErrors(e instanceof Error ? [e.message] : ["שגיאה בכניסה, נא לנסות שוב מאוחר יותר"]);
      return;
    }
    finally {
      setLoading(false)
    }
  }

  const handleChange = (field: string, value: string) => {
    setUserLogin((prev) => ({ ...prev, [field]: value }))
    // Clear errors when user starts typing
    if (errors.length > 0) {
      setErrors([])
    }
  }

  return (
    <ConfigProvider direction="rtl">
      <CenteredLayout>
        <Card
          className="login-form"
          style={{
            maxWidth: 500,
            width: "100%",
            padding: 24,
            boxShadow: "0 4px 12px rgba(0, 0, 0, 0.1)",
            borderRadius: 12,
            border: "none",
          }}
        >
          <div style={{
            textAlign: "center", 
            marginBottom: 32,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            flexDirection: "column"
          }}>
            <Avatar
              size={80}
              style={{
                background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)",
                marginBottom: 16,
                boxShadow: "0 4px 14px rgba(102, 126, 234, 0.4)",
              }}
              icon={<CrownOutlined style={{ fontSize: 40 }} />}
            />


            <Title level={2} style={{ marginBottom: 8, color: "#2d3748", textAlign: "center" }}>
              כניסת משתמש
            </Title>

            <Text type="secondary" style={{ fontSize: 16 }}>
              ברוכים הבאים למערכת ניהול העסק
            </Text>
          </div>

          <Form layout="vertical" onFinish={handleSubmit}>
            <Form.Item
              label="אימייל"
              required
              style={{ marginBottom: 20 }}
            >
              <Input
                prefix={<MailOutlined style={{ color: "#667eea" }} />}
                placeholder="הזן את האימייל שלך"
                size="large"
                value={userLogin.email}
                onChange={(e) => handleChange("email", e.target.value)}
                style={{ borderRadius: 10 }}
              />
            </Form.Item>

            <Form.Item
              label="סיסמא"
              required
              style={{ marginBottom: 24 }}
            >
              <Input.Password
                prefix={<LockOutlined style={{ color: "#667eea" }} />}
                placeholder="הזן את הסיסמה שלך"
                size="large"
                value={userLogin.password}
                onChange={(e) => handleChange("password", e.target.value)}
                style={{ borderRadius: 10 }}
              />
            </Form.Item>

            <Button
              type="primary"
              htmlType="submit"
              size="large"
              loading={loading}
              disabled={loading}
              icon={<LoginOutlined />}
              block
              style={{
                height: 48,
                borderRadius: 10,
                fontWeight: 600,
                fontSize: 16,
                marginBottom: 16,
              }}
            >
              {loading ? "מתחבר..." : "התחבר"}
            </Button>

            <Divider>
              <Text type="secondary">או</Text>
            </Divider>

            <Button
              type="default"
              size="large"
              block
              onClick={() => nav("/register-user")}
              style={{
                height: 48,
                borderRadius: 10,
                fontWeight: 600,
                marginBottom: 16,
                borderWidth: 2,
              }}
            >
              הירשם עכשיו
            </Button>

            <div style={{ textAlign: "center" }}>
              <Space>
                <Text type="secondary">מנהל?</Text>
                <Link href="/admin-login" style={{ fontWeight: 600 }}>
                  התחבר כמנהל
                </Link>
              </Space>
            </div>

            {errors.length > 0 && (
              <div style={{ marginTop: 16 }}>
                {errors.map((error, index) => (
                  <Alert
                    key={index}
                    message="שגיאה בהתחברות"
                    description={error}
                    type="error"
                    showIcon
                    icon={<ExclamationCircleOutlined />}
                    style={{
                      marginBottom: 8,
                      borderRadius: 8,
                      background: "linear-gradient(135deg, #fff2f0 0%, #ffebe8 100%)",
                      border: "1px solid #ffccc7",
                    }}
                  />
                ))}
              </div>
            )}
          </Form>
        </Card>
      </CenteredLayout>
    </ConfigProvider>
  )
}

export default UserLogin
